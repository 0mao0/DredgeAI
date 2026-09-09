using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>
/// meeting-bot（FastAPI，端口 8101）HTTP 客户端。
/// 所有请求携带 X-Meeting-Bot-Key；配置节 MeetingBot:BaseUrl / MeetingBot:Key。
/// </summary>
public class MeetingBotClient : IMeetingBotClient, ITransientDependency
{
    private const int TranscribeTimeoutSeconds = 180;
    private const int TranscribePollMs = 1500;
    /// <summary>DGX TTS 并发闸门很小（实测 3），429 属瞬时拥塞：退避重试次数</summary>
    private const int DgxBusyAttempts = 3;
    /// <summary>429 重试耗尽后的错误码：调用方据此快速失败重试，而不是回退本地合成</summary>
    private const string DgxBusyErrorCode = "DGX_TTS_BUSY";
    /// <summary>DGX 未在响应头声明采样率时的兜底值（实测当前服务为 24000Hz）</summary>
    private const int DefaultDgxSampleRate = 24000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MeetingBotOptions _options;
    private readonly ILogger<MeetingBotClient> _logger;

    public MeetingBotClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MeetingBotOptions> options,
        ILogger<MeetingBotClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> AsrAsync(byte[] audio, CancellationToken ct = default)
    {
        var dgx = _options.DgxAsr;
        if (IsDgxAsrConfigured(dgx))
        {
            try
            {
                return await DgxAsrAsync(audio, dgx, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DGX ASR 失败，回退 meeting-bot（BaseUrl={BaseUrl}）", _options.BaseUrl);
            }
        }

        using var form = BuildForm();
        using var audioContent = new ByteArrayContent(audio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(audioContent, "audio", "audio.bin");

        using var response = await CreateClient().PostAsync("/asr", form, ct);
        await EnsureSuccessAsync(response, "ASR", ct);
        var payload = await response.Content.ReadFromJsonAsync<AsrResponse>(JsonOptions, ct);
        return payload?.Text ?? throw new BusinessException("MEETING_BOT_ASR_FAILED", "ASR 响应缺少 text");
    }

    /// <summary>DGX ASR（OpenAI 兼容 /audio/transcriptions，model=qwen3-asr）。</summary>
    private async Task<string> DgxAsrAsync(byte[] audio, DgxAsrOptions dgx, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(dgx.Model ?? "qwen3-asr"), "model");
        form.Add(new StringContent("auto"), "language");
        using var audioContent = new ByteArrayContent(audio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "file", "audio.wav");

        using var request = new HttpRequestMessage(HttpMethod.Post, dgx.BaseUrl!.TrimEnd('/') + "/audio/transcriptions")
        {
            Content = form
        };
        AddDgxAuth(request);
        using var client = _httpClientFactory.CreateClient(); // 未命名客户端：不带 meeting-bot 默认请求头
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        await EnsureDgxSuccessAsync(response, "ASR", ct);
        var payload = await response.Content.ReadFromJsonAsync<AsrResponse>(JsonOptions, ct);
        return payload?.Text ?? throw new BusinessException("DGX_ASR_FAILED", "DGX ASR 响应缺少 text");
    }

    public async Task<byte[]> TtsAsync(string text, CancellationToken ct = default)
    {
        // 1) DGX Qwen3-TTS（最高优先级）
        if (IsDgxTtsConfigured(_options.DgxQwenTts))
        {
            try
            {
                return await DgxSynthesizeAsync(text, _options.DgxQwenTts, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 调用方主动取消（前端停掉预取/断开）：不当作失败，更不能回退本地再合成一遍
                throw;
            }
            catch (BusinessException ex) when (ex.Code == DgxBusyErrorCode)
            {
                // 上游并发已满：本地兜底更慢且开发环境常未启动，直接让调用方稍后重试
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DGX Qwen3-TTS 合成失败，回退本地 CosyVoice（BaseUrl={BaseUrl}）", _options.BaseUrl);
            }
        }

        // 2) 本地 CosyVoice（兜底）
        using var response = await CreateClient().PostAsJsonAsync(
            "/tts", new { text }, JsonOptions, ct);
        await EnsureSuccessAsync(response, "TTS", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>DGX TTS 合成（Qwen3，OpenAI 兼容 /audio/speech 非流式）。</summary>
    private async Task<byte[]> DgxSynthesizeAsync(string text, DgxTtsOptions dgx, CancellationToken ct)
    {
        var url = dgx.BaseUrl!.TrimEnd('/') + "/audio/speech";
        object payload = new
        {
            model = dgx.Model ?? "qwen3-tts",
            input = text,
            voice = dgx.Voice ?? "serena",
            response_format = "wav"
        };

        // 裸客户端 + 长超时：工厂客户端带 resilience 60s AttemptTimeout，
        // 整段合成（长文本 3~4 分钟）会被超时强杀；裸客户端无重试/超时包装
        using var client = new HttpClient(CreateDgxStreamHandler())
        {
            Timeout = TimeSpan.FromSeconds(300)
        };

        // 429（并发闸门满）是瞬时拥塞：退避重试，比回退本地合成（更慢，开发环境常未启动）划算
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            AddDgxAuth(request);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < DgxBusyAttempts)
            {
                _logger.LogWarning("DGX TTS 并发已满（429），第 {Attempt} 次退避重试", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
                continue;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new BusinessException(DgxBusyErrorCode, "语音合成服务繁忙，请稍后重试");
            }
            await EnsureDgxSuccessAsync(response, "TTS", ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0)
            {
                throw new BusinessException("DGX_TTS_EMPTY_AUDIO", "DGX TTS 返回空音频");
            }
            return EnsureWavContainer(bytes, response);
        }
    }

    /// <summary>流式 TTS 直通（53fa 官方页同款）：POST {base}/audio/speech，
    /// stream=true + emit_frames=4，上游以 chunked audio/pcm 返回原始 PCM（23040Hz/16bit/单声道），
    /// 本方法**零加工原样转发**：不做停顿压缩、不裁剪静音、不拼接帧——
    /// 上游 emit_frames=4 的句间停顿本来就自然，任何加工只会破坏节奏并引入拼接爆音。
    /// 客户端断开时静默结束；上游中断则异常自然冒泡，由 HTTP 层中止响应，
    /// 前端据此感知流不完整并回退逐段合成。</summary>
    public async Task StreamTtsAsync(
        string text,
        Stream destination,
        CancellationToken ct = default,
        Action<int>? onSampleRate = null)
    {
        var dgx = _options.DgxQwenTts;
        if (!IsDgxTtsConfigured(dgx))
        {
            throw new BusinessException("TTS_STREAM_NOT_CONFIGURED", "流式语音合成未配置");
        }

        var url = dgx!.BaseUrl!.TrimEnd('/') + "/audio/speech";
        var payload = new
        {
            input = text,
            voice = dgx.Voice ?? "serena",
            stream = true,
            emit_frames = 4,
        };

        // 裸客户端：不走 IHttpClientFactory——resilience 的 60s AttemptTimeout 会强杀长文本流、
        // 重试会拼接两次响应产生满幅噪声；不复用连接池，避免旧 keep-alive 连接的脏状态。
        using var client = new HttpClient(CreateDgxStreamHandler())
        {
            Timeout = TimeSpan.FromSeconds(300)
        };

        // 429（并发闸门满）出现在响应头阶段：此时还没写出任何音频，退避重试是安全的
        var watch = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage response;
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            AddDgxAuth(request);
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < DgxBusyAttempts)
            {
                _logger.LogWarning("DGX TTS 流并发已满（429），第 {Attempt} 次退避重试", attempt);
                response.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
                continue;
            }
            break;
        }
        var headersMs = watch.ElapsedMilliseconds;

        using (response)
        {
            await EnsureDgxSuccessAsync(response, "TTS stream", ct);
            // 采样率以响应头为准（实测 24000Hz）：写死会让前端 4% 变速走音
            var sampleRate = ReadSampleRate(response);
            onSampleRate?.Invoke(sampleRate);

            await using var upstream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[8192];
            long? firstByteMs = null;
            long totalBytes = 0;
            try
            {
                int read;
                while ((read = await upstream.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    firstByteMs ??= watch.ElapsedMilliseconds;
                    totalBytes += read;
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                    await destination.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 客户端已断开/主动停止：静默结束，已播内容保留
            }
            finally
            {
                // 便于定位上游抖动：响应头延迟、首块延迟与有效吞吐（24kHz·16bit 单声道下 48000B/s ≈ 1x 实时）
                var audioSeconds = totalBytes / 2.0 / sampleRate;
                _logger.LogInformation(
                    "DGX TTS 流：响应头 {HeadersMs}ms，首块 {FirstByteMs}ms，{TotalBytes} 字节（{AudioSeconds:F1}s 音频），总耗时 {TotalMs}ms，约 {Speed:F2}x 实时",
                    headersMs,
                    firstByteMs ?? -1,
                    totalBytes,
                    audioSeconds,
                    watch.ElapsedMilliseconds,
                    watch.ElapsedMilliseconds > 0 ? audioSeconds * 1000 / watch.ElapsedMilliseconds : 0);
            }
            // 上游中断（EOF 提前/连接重置/超时）：不吞——冒泡中止响应，前端据此回退
        }
    }

    /// <summary>
    /// DGX 流式专用裸 HttpClient 处理器：不走 IHttpClientFactory（无 resilience/服务发现包装），
    /// 不复用连接池（每次新连接，避免旧 keep-alive 连接的脏状态）。
    /// </summary>
    private static SocketsHttpHandler CreateDgxStreamHandler()
    {
        return new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = TimeSpan.Zero,
            PooledConnectionLifetime = TimeSpan.Zero,
            MaxConnectionsPerServer = 8,
            AutomaticDecompression = System.Net.DecompressionMethods.None
        };
    }

    public async Task<List<FaceMatchDto>> RecognizeAsync(byte[] image, CancellationToken ct = default)
    {
        using var form = BuildForm();
        using var imageContent = new ByteArrayContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(imageContent, "image", "face.jpg");

        using var response = await CreateClient().PostAsync("/recognize", form, ct);
        await EnsureSuccessAsync(response, "人脸识别", ct);
        var payload = await response.Content.ReadFromJsonAsync<RecognizeResponse>(JsonOptions, ct);
        return payload?.Faces ?? [];
    }

    public async Task<int> CountAsync(byte[] image, CancellationToken ct = default)
    {
        using var form = BuildForm();
        using var imageContent = new ByteArrayContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(imageContent, "image", "scene.jpg");

        using var response = await CreateClient().PostAsync("/count", form, ct);
        await EnsureSuccessAsync(response, "人数统计", ct);
        var payload = await response.Content.ReadFromJsonAsync<CountResponse>(JsonOptions, ct);
        return payload?.Count ?? 0;
    }

    public async Task EnrollAsync(string workerId, string name, byte[] image, CancellationToken ct = default)
    {
        using var form = BuildForm();
        form.Add(new StringContent(workerId), "worker_id");
        form.Add(new StringContent(name ?? ""), "name");
        using var imageContent = new ByteArrayContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(imageContent, "image", "face.jpg");

        using var response = await CreateClient().PostAsync("/enroll", form, ct);
        await EnsureSuccessAsync(response, "人脸注册", ct);
    }

    public async Task<string> TranscribeAsync(byte[] audio, CancellationToken ct = default)
    {
        using var form = BuildForm();
        using var audioContent = new ByteArrayContent(audio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(audioContent, "audio", "recording.bin");

        using (var response = await CreateClient().PostAsync("/transcribe", form, ct))
        {
            await EnsureSuccessAsync(response, "提交转写", ct);
            var start = await response.Content.ReadFromJsonAsync<TranscribeStartResponse>(JsonOptions, ct);
            var jobId = start?.JobId
                ?? throw new BusinessException("MEETING_BOT_TRANSCRIBE_FAILED", "转写提交响应缺少 job_id");

            var deadline = DateTime.UtcNow.AddSeconds(TranscribeTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                var job = await CreateClient().GetFromJsonAsync<TranscribeJobResponse>(
                    $"/transcribe/{jobId}", JsonOptions, ct);
                if (job?.Status == "done")
                {
                    return job.Text ?? "";
                }
                if (job?.Status is "failed" or "not_found")
                {
                    throw new BusinessException("MEETING_BOT_TRANSCRIBE_FAILED", $"转写任务失败: {job?.Status}");
                }
                await Task.Delay(TranscribePollMs, ct);
            }
        }

        throw new BusinessException("MEETING_BOT_TRANSCRIBE_TIMEOUT", "转写超时（180s）");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(MeetingBotClient));
        return client;
    }

    private static bool IsDgxTtsConfigured(DgxTtsOptions? dgx)
        => dgx is not null && !string.IsNullOrWhiteSpace(dgx.BaseUrl);

    private static bool IsDgxAsrConfigured(DgxAsrOptions? dgx)
        => dgx is not null && !string.IsNullOrWhiteSpace(dgx.BaseUrl);

    /// <summary>DGX 调用统一错误处理：非 2xx 时记录响应体并抛业务异常。</summary>
    private async Task EnsureDgxSuccessAsync(HttpResponseMessage response, string label, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("DGX {Label} 失败（{Status}）：{Body}",
            label, (int)response.StatusCode, Truncate(body, 500));
        throw new BusinessException("DGX_CALL_FAILED", $"DGX {label} 调用失败（HTTP {(int)response.StatusCode}）");
    }

    private void AddDgxAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.DgxApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.DgxApiKey);
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    /// <summary>DGX /audio/speech 返回的音频：优先按 WAV 处理（response_format=wav 时），
    /// 若上游给的是裸 PCM（历史行为/未按 wav 返回）则补 WAV 头，
    /// 否则下游按 RIFF 解析会失败（缓存文件、HTMLAudio 播放、WAV 合并）。</summary>
    private static byte[] EnsureWavContainer(byte[] payload, HttpResponseMessage response)
    {
        if (payload.Length >= 4
            && payload[0] == (byte)'R' && payload[1] == (byte)'I'
            && payload[2] == (byte)'F' && payload[3] == (byte)'F')
        {
            return payload;
        }
        return WrapPcmAsWav(payload, ReadSampleRate(response));
    }

    /// <summary>读取 DGX 响应头声明的采样率（缺省 24000，实测当前服务返回 24000Hz）。</summary>
    private static int ReadSampleRate(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-sample-rate", out var values)
            && int.TryParse(values.FirstOrDefault(), out var sampleRate)
            && sampleRate > 0)
        {
            return sampleRate;
        }
        return DefaultDgxSampleRate;
    }

    /// <summary>裸 PCM（16bit 单声道）包标准 WAV 头。</summary>
    private static byte[] WrapPcmAsWav(byte[] pcm, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int channels = 1;
        var header = new byte[44];
        void WriteAscii(int offset, string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                header[offset + i] = (byte)text[i];
            }
        }

        WriteAscii(0, "RIFF");
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), 36 + pcm.Length);
        WriteAscii(8, "WAVE");
        WriteAscii(12, "fmt ");
        BitConverter.TryWriteBytes(header.AsSpan(16, 4), 16);
        BitConverter.TryWriteBytes(header.AsSpan(20, 2), (short)1);
        BitConverter.TryWriteBytes(header.AsSpan(22, 2), (short)channels);
        BitConverter.TryWriteBytes(header.AsSpan(24, 4), sampleRate);
        BitConverter.TryWriteBytes(header.AsSpan(28, 4), sampleRate * channels * bitsPerSample / 8);
        BitConverter.TryWriteBytes(header.AsSpan(32, 2), (short)(channels * bitsPerSample / 8));
        BitConverter.TryWriteBytes(header.AsSpan(34, 2), (short)bitsPerSample);
        WriteAscii(36, "data");
        BitConverter.TryWriteBytes(header.AsSpan(40, 4), pcm.Length);

        var wav = new byte[44 + pcm.Length];
        header.CopyTo(wav, 0);
        pcm.CopyTo(wav, 44);
        return wav;
    }

    private static MultipartFormDataContent BuildForm()
    {
        var form = new MultipartFormDataContent();
        return form;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string label, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogWarning("meeting-bot {Label} 失败（{Status}）：{Body}",
            label, (int)response.StatusCode, body.Length <= 500 ? body : body[..500]);
        throw new BusinessException("MEETING_BOT_CALL_FAILED", $"meeting-bot {label} 调用失败");
    }

    private class AsrResponse
    {
        public string? Text { get; set; }
    }

    private class RecognizeResponse
    {
        public List<FaceMatchDto>? Faces { get; set; }
    }

    private class CountResponse
    {
        public int? Count { get; set; }
    }

    private class TranscribeStartResponse
    {
        public string? JobId { get; set; }
    }

    private class TranscribeJobResponse
    {
        public string? Status { get; set; }

        public string? Text { get; set; }
    }
}

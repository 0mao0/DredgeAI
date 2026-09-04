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

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        AddDgxAuth(request);
        // 裸客户端 + 长超时：工厂客户端带 resilience 60s AttemptTimeout，
        // 整段合成（长文本 3~4 分钟）会被超时强杀；裸客户端无重试/超时包装
        using var client = new HttpClient(CreateDgxStreamHandler())
        {
            Timeout = TimeSpan.FromSeconds(300)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        await EnsureDgxSuccessAsync(response, "TTS", ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0)
        {
            throw new BusinessException("DGX_TTS_EMPTY_AUDIO", "DGX TTS 返回空音频");
        }
        return bytes;
    }

    /// <summary>流式 TTS 直通（53fa 官方页同款）：POST {base}/audio/speech，
    /// stream=true + emit_frames=4，上游以 chunked audio/pcm 返回原始 PCM（23040Hz/16bit/单声道），
    /// 本方法**零加工原样转发**：不做停顿压缩、不裁剪静音、不拼接帧——
    /// 上游 emit_frames=4 的句间停顿本来就自然，任何加工只会破坏节奏并引入拼接爆音。
    /// 客户端断开时静默结束；上游中断则异常自然冒泡，由 HTTP 层中止响应，
    /// 前端据此感知流不完整并回退逐段合成。</summary>
    public async Task StreamTtsAsync(string text, Stream destination, CancellationToken ct = default)
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

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        AddDgxAuth(request);

        // 裸客户端：不走 IHttpClientFactory——resilience 的 60s AttemptTimeout 会强杀长文本流、
        // 重试会拼接两次响应产生满幅噪声；不复用连接池，避免旧 keep-alive 连接的脏状态。
        using var client = new HttpClient(CreateDgxStreamHandler())
        {
            Timeout = TimeSpan.FromSeconds(300)
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureDgxSuccessAsync(response, "TTS stream", ct);

        await using var upstream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[8192];
        try
        {
            int read;
            while ((read = await upstream.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                await destination.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 客户端已断开/主动停止：静默结束，已播内容保留
        }
        // 上游中断（EOF 提前/连接重置/超时）：不吞——冒泡中止响应，前端据此回退
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

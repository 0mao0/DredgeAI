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
    private const string TencentTtsHost = "tts.tencentcloudapi.com";
    private const string TencentTtsService = "tts";
    private const string TencentTtsAction = "TextToVoice";
    private const string TencentTtsVersion = "2019-08-23";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>腾讯云 API 3.0 请求体：保持 PascalCase 属性名，非 ASCII 不转义（官方 SDK 均发送原始 UTF-8）。</summary>
    private static readonly JsonSerializerOptions TencentJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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
        using var form = BuildForm();
        using var audioContent = new ByteArrayContent(audio);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(audioContent, "audio", "audio.bin");

        using var response = await CreateClient().PostAsync("/asr", form, ct);
        await EnsureSuccessAsync(response, "ASR", ct);
        var payload = await response.Content.ReadFromJsonAsync<AsrResponse>(JsonOptions, ct);
        return payload?.Text ?? throw new BusinessException("MEETING_BOT_ASR_FAILED", "ASR 响应缺少 text");
    }

    public async Task<byte[]> TtsAsync(string text, CancellationToken ct = default)
    {
        var cloud = _options.CloudTts;
        if (IsCloudTtsConfigured(cloud))
        {
            try
            {
                return await SynthesizeWithTencentCloudAsync(text, cloud!, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "腾讯云 TTS 合成失败，回退本地 CosyVoice（BaseUrl={BaseUrl}）", _options.BaseUrl);
            }
        }

        using var response = await CreateClient().PostAsJsonAsync(
            "/tts", new { text }, JsonOptions, ct);
        await EnsureSuccessAsync(response, "TTS", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>流式 TTS：把 meeting-bot 的帧流（4 字节大端长度 + WAV）持续写入 destination。</summary>
    public async Task StreamTtsAsync(string text, Stream destination, CancellationToken ct = default)
    {
        var cloud = _options.CloudTts;
        if (IsCloudTtsConfigured(cloud))
        {
            try
            {
                // 云端整段合成（约 1~3 秒）完成后一次性吐帧，播放端收到整段直接播放，不再逐句等待。
                var wav = await SynthesizeWithTencentCloudAsync(text, cloud!, ct);
                await WriteFrameAsync(destination, wav, ct);
                await WriteFrameAsync(destination, [], ct);
                await destination.FlushAsync(ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "腾讯云 TTS 流式合成失败，回退本地 CosyVoice");
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/tts/stream")
        {
            Content = JsonContent.Create(new { text }, options: JsonOptions)
        };
        using var response = await CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessAsync(response, "TTS 流式", ct);
        await using var upstream = await response.Content.ReadAsStreamAsync(ct);
        await upstream.CopyToAsync(destination, ct);
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

    private static bool IsCloudTtsConfigured(CloudTtsOptions? cloud)
        => cloud is not null
           && !string.IsNullOrWhiteSpace(cloud.SecretId)
           && !string.IsNullOrWhiteSpace(cloud.SecretKey);

    /// <summary>腾讯云 TextToVoice 合成：按 ≤MaxTextChars 断句分片，限流并发，最后拼接成整段 WAV。</summary>
    private async Task<byte[]> SynthesizeWithTencentCloudAsync(string text, CloudTtsOptions cloud, CancellationToken ct)
    {
        var chunks = SplitForCloud(text, cloud.MaxTextChars);
        if (chunks.Count == 0)
        {
            throw new BusinessException("TENCENT_TTS_EMPTY_TEXT", "语音合成文本为空");
        }

        var wavs = new byte[chunks.Count][];
        using var semaphore = new SemaphoreSlim(cloud.MaxConcurrency > 0 ? cloud.MaxConcurrency : 1);
        var tasks = chunks.Select(async (chunk, i) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                wavs[i] = await TencentTextToVoiceAsync(chunk, cloud, ct);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();
        await Task.WhenAll(tasks);

        return MeetingRecordAppService.ConcatWavs(wavs);
    }

    private async Task<byte[]> TencentTextToVoiceAsync(string text, CloudTtsOptions cloud, CancellationToken ct)
    {
        var payload = new
        {
            Text = text,
            SessionId = Guid.NewGuid().ToString("D"),
            Volume = cloud.Volume,
            Speed = cloud.Speed,
            ProjectId = 0,
            ModelType = 1,
            VoiceType = cloud.VoiceType,
            PrimaryLanguage = 1,
            SampleRate = cloud.SampleRate,
            Codec = "wav"
        };
        var json = JsonSerializer.Serialize(payload, TencentJsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{TencentTtsHost}/")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Host", TencentTtsHost);
        request.Headers.TryAddWithoutValidation("X-TC-Action", TencentTtsAction);
        request.Headers.TryAddWithoutValidation("X-TC-Version", TencentTtsVersion);
        request.Headers.TryAddWithoutValidation("X-TC-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(cloud.Region))
        {
            request.Headers.TryAddWithoutValidation("X-TC-Region", cloud.Region);
        }
        request.Headers.TryAddWithoutValidation("Authorization", BuildTencentAuthorization(cloud, json, timestamp));

        using var client = _httpClientFactory.CreateClient(); // 未命名客户端：不带 meeting-bot 默认请求头
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("腾讯云 TTS HTTP {Status}：{Body}",
                (int)response.StatusCode, Truncate(errorBody, 500));
            throw new BusinessException("TENCENT_TTS_HTTP_FAILED", $"腾讯云 TTS 调用失败（HTTP {(int)response.StatusCode}）");
        }

        return ParseTencentAudioResponse(await response.Content.ReadAsStringAsync(ct));
    }

    private static byte[] ParseTencentAudioResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Response", out var response))
        {
            throw new BusinessException("TENCENT_TTS_INVALID_RESPONSE", "腾讯云 TTS 返回格式异常");
        }
        if (response.TryGetProperty("Error", out var error))
        {
            var code = error.TryGetProperty("Code", out var codeEl) ? codeEl.GetString() : "Unknown";
            var message = error.TryGetProperty("Message", out var msgEl) ? msgEl.GetString() : "";
            throw new BusinessException("TENCENT_TTS_FAILED", $"腾讯云 TTS 失败：{code} {message}");
        }
        if (!response.TryGetProperty("Audio", out var audio) || audio.ValueKind != JsonValueKind.String)
        {
            throw new BusinessException("TENCENT_TTS_NO_AUDIO", "腾讯云 TTS 响应缺少 Audio");
        }
        return Convert.FromBase64String(audio.GetString()!);
    }

    /// <summary>TC3-HMAC-SHA256 签名（腾讯云 API 3.0）。</summary>
    private static string BuildTencentAuthorization(CloudTtsOptions cloud, string payload, long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp)
            .UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{TencentTtsHost}\n";
        const string signedHeaders = "content-type;host";
        var canonicalRequest = $"POST\n/\n\n{canonicalHeaders}\n{signedHeaders}\n{Sha256Hex(payload)}";
        var credentialScope = $"{date}/{TencentTtsService}/tc3_request";
        var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";

        var secretDate = HmacSha256(Encoding.UTF8.GetBytes("TC3" + cloud.SecretKey), date);
        var secretService = HmacSha256(secretDate, TencentTtsService);
        var secretSigning = HmacSha256(secretService, "tc3_request");
        var signature = HexLower(HmacSha256(secretSigning, stringToSign));

        return $"TC3-HMAC-SHA256 Credential={cloud.SecretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private static string Sha256Hex(string input)
        => HexLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    private static byte[] HmacSha256(byte[] key, string input)
        => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(input));

    private static string HexLower(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>按句末标点断句，超过 maxChars 强制截断（接口上限 150 汉字）。</summary>
    private static List<string> SplitForCloud(string text, int maxChars)
    {
        if (maxChars <= 0)
        {
            maxChars = 140;
        }

        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            current.Append(ch);
            var isSentenceEnd = ch is '。' or '！' or '？' or '；' or '…' or '\n';
            if (current.Length >= maxChars || isSentenceEnd)
            {
                var segment = current.ToString().Trim();
                if (segment.Length > 0)
                {
                    result.Add(segment);
                }
                current.Clear();
            }
        }
        var tail = current.ToString().Trim();
        if (tail.Length > 0)
        {
            result.Add(tail);
        }
        return result;
    }

    private static async Task WriteFrameAsync(Stream destination, byte[] payload, CancellationToken ct)
    {
        var header = new byte[4];
        header[0] = (byte)(payload.Length >> 24);
        header[1] = (byte)((payload.Length >> 16) & 0xFF);
        header[2] = (byte)((payload.Length >> 8) & 0xFF);
        header[3] = (byte)(payload.Length & 0xFF);
        await destination.WriteAsync(header, 0, header.Length, ct);
        if (payload.Length > 0)
        {
            await destination.WriteAsync(payload, 0, payload.Length, ct);
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

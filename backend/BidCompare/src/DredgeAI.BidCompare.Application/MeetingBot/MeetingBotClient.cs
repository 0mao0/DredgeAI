using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        using var response = await CreateClient().PostAsJsonAsync(
            "/tts", new { text }, JsonOptions, ct);
        await EnsureSuccessAsync(response, "TTS", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
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

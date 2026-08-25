using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("meeting")]
[Route("api/meeting/records")]
public class MeetingRecordController : AbpControllerBase
{
    private readonly IMeetingRecordAppService _service;
    private readonly IMeetingBotClient _bot;

    public MeetingRecordController(IMeetingRecordAppService service, IMeetingBotClient bot)
    {
        _service = service;
        _bot = bot;
    }

    [HttpPost]
    public Task<MeetingRecordDto> Create([FromBody] PreInfoInput input)
        => _service.CreateAsync(input);

    [HttpGet]
    public Task<List<MeetingHistoryDto>> History(int maxCount = 20)
        => _service.GetHistoryAsync(maxCount);

    [HttpPost("~/api/meeting/parse-plan")]
    public Task<PlanParseResult> ParsePlan([FromBody] PlanParseInput input)
        => _service.ParsePlanAsync(input.PlanText);

    [HttpGet("{id:guid}")]
    public Task<MeetingRecordDto> Get(Guid id)
        => _service.GetAsync(id);

    [HttpPost("{id:guid}/speech/generate")]
    public Task<SpeechDraftDto> GenerateSpeech(Guid id)
        => _service.GenerateSpeechAsync(id);

    [HttpGet("{id:guid}/speech")]
    public Task<SpeechDraftDto?> GetSpeech(Guid id)
        => _service.GetSpeechAsync(id);

    [HttpPut("{id:guid}/speech")]
    public Task<SpeechDraftDto> UpdateSpeech(Guid id, [FromBody] UpdateSpeechInput input)
        => _service.UpdateSpeechAsync(id, input.Content);

    [HttpGet("{id:guid}/speech/audio")]
    public async Task<IActionResult> SpeechAudio(Guid id)
    {
        var audio = await _service.GetSpeechAudioAsync(id);
        return File(audio, "audio/wav");
    }

    [HttpGet("{id:guid}/speech/audio/status")]
    public async Task<MeetingBot.SpeechAudioStatusDto> SpeechAudioStatus(Guid id)
    {
        return new MeetingBot.SpeechAudioStatusDto
        {
            Cached = await _service.IsSpeechAudioCachedAsync(id),
            LeadCached = await _service.IsSpeechLeadAudioCachedAsync(id),
            LeadText = await _service.GetSpeechLeadTextAsync(id)
        };
    }

    [HttpGet("{id:guid}/speech/audio/lead")]
    public async Task<IActionResult> SpeechLeadAudio(Guid id)
    {
        var audio = await _service.GetSpeechLeadAudioAsync(id);
        return audio is null ? NotFound() : File(audio, "audio/wav");
    }

    [HttpGet("{id:guid}/speech/audio/segment/{index:int}")]
    public async Task<IActionResult> SpeechSegmentAudio(Guid id, int index)
    {
        var audio = await _service.GetSpeechSegmentAudioAsync(id, index);
        return audio is null ? NotFound() : File(audio, "audio/wav");
    }

    [HttpPost("{id:guid}/speech/audio/cache")]
    public async Task SaveSpeechAudioCache(Guid id, [FromForm] IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        await _service.SaveSpeechAudioCacheAsync(id, ms.ToArray());
    }

    [HttpPost("{id:guid}/start")]
    public Task<MeetingRecordDto> Start(Guid id)
        => _service.StartAsync(id);

    [HttpPost("{id:guid}/attendance/recognize")]
    public async Task<AttendanceRecognizeResult> Recognize(Guid id, [FromForm] IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var faces = await _service.RecognizeAttendanceAsync(id, ms.ToArray());
        var count = await _bot.CountAsync(ms.ToArray());
        return new AttendanceRecognizeResult { Faces = faces, Count = count };
    }

    [HttpPost("{id:guid}/unrecognized-faces")]
    public async Task<int> SaveUnrecognizedFaces(Guid id, [FromForm] List<IFormFile> files, [FromForm] string? metadata)
    {
        var items = new List<(byte[] Data, double Confidence, double[] Bbox)>();
        var parsed = ParseUnrecognizedMetadata(metadata);
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length == 0)
            {
                continue;
            }
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var meta = parsed.Count > i ? parsed[i] : (Confidence: 0, Bbox: Array.Empty<double>());
            items.Add((ms.ToArray(), meta.Confidence, meta.Bbox));
        }
        return await _service.SaveUnrecognizedFacesAsync(id, items);
    }

    [HttpGet("{id:guid}/attendance")]
    public Task<List<AttendanceItemDto>> Attendance(Guid id)
        => _service.GetAttendanceAsync(id);

    [HttpPost("{id:guid}/qa")]
    public Task<QaRecordDto> AskQa(Guid id, [FromBody] AskQaInput input)
        => _service.AskQaAsync(id, input.Question);

    [HttpPost("{id:guid}/qa/audio")]
    public async Task<QaRecordDto> AskQaAudio(Guid id, [FromForm] IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var text = await _bot.AsrAsync(ms.ToArray());
        return await _service.AskQaAsync(id, text);
    }

    [HttpGet("~/api/meeting/qa/{qaId:guid}/audio")]
    public async Task<IActionResult> QaAudio(Guid qaId)
    {
        var audio = await _service.GetQaAudioAsync(qaId);
        return File(audio, "audio/wav");
    }

    [HttpPost("~/api/meeting/asr")]
    public async Task<string> Asr([FromForm] IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        return await _bot.AsrAsync(ms.ToArray());
    }

    [HttpPost("~/api/meeting/tts")]
    public async Task<IActionResult> Tts([FromBody] TtsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return BadRequest();
        }
        var audio = await _bot.TtsAsync(input.Text);
        return File(audio, "audio/wav");
    }

    [HttpPost("~/api/meeting/tts/stream")]
    public async Task TtsStream([FromBody] TtsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        Response.ContentType = "application/octet-stream";
        await _bot.StreamTtsAsync(input.Text, Response.Body, HttpContext.RequestAborted);
    }

    [HttpPost("{id:guid}/recording")]
    public async Task<MeetingRecordDto> SaveRecording(Guid id, [FromForm] IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        return await _service.SaveRecordingAsync(id, ms.ToArray(), audio.FileName);
    }

    [HttpPost("{id:guid}/complete")]
    public Task<MeetingRecordDto> Complete(Guid id)
        => _service.CompleteAsync(id);

    [HttpGet("{id:guid}/report")]
    public Task<ReportDto?> Report(Guid id)
        => _service.GetReportAsync(id);

    public class AttendanceRecognizeResult
    {
        public List<AttendanceItemDto> Faces { get; set; } = new();

        public int Count { get; set; }
    }

    public class TtsInput
    {
        public string Text { get; set; } = "";
    }

    private static List<(double Confidence, double[] Bbox)> ParseUnrecognizedMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return [];
        }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var result = new List<(double Confidence, double[] Bbox)>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var confidence = item.TryGetProperty("confidence", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? c.GetDouble()
                    : 0;
                var bbox = item.TryGetProperty("bbox", out var b) && b.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? b.EnumerateArray().Select(x => x.GetDouble()).ToArray()
                    : Array.Empty<double>();
                result.Add((confidence, bbox));
            }
            return result;
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}

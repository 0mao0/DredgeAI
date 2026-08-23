using System;
using System.Collections.Generic;
using System.IO;
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

    [HttpPost("{id:guid}/start")]
    public Task<MeetingRecordDto> Start(Guid id)
        => _service.StartAsync(id);

    [HttpPost("{id:guid}/attendance/recognize")]
    public async Task<AttendanceRecognizeResult> Recognize(Guid id, [FromForm] IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var faces = await _service.RecognizeAttendanceAsync(id, ms.ToArray());
        return new AttendanceRecognizeResult { Faces = faces };
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
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>会议记录接口</summary>
[Authorize]
[Route("api/bidcompare/meeting-records")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("会议记录")]
public class MeetingRecordController : BidCompareController
{
    private readonly IMeetingRecordAppService _service;
    private readonly IMeetingBotClient _bot;
    private readonly ISpeechDraftStreamer _streamer;

    public MeetingRecordController(IMeetingRecordAppService service, IMeetingBotClient bot, ISpeechDraftStreamer streamer)
    {
        _service = service;
        _bot = bot;
        _streamer = streamer;
    }

    /// <summary>创建会议记录</summary>
    /// <param name="input">会议预登记信息</param>
    /// <returns>创建成功的会议记录</returns>
    [HttpPost]
    public Task<MeetingRecordDto> CreateAsync([FromBody] PreInfoInput input)
        => _service.CreateAsync(input);

    /// <summary>获取会议历史记录列表</summary>
    /// <param name="maxCount">最大返回条数，默认 20</param>
    /// <returns>会议历史记录列表</returns>
    [HttpGet]
    public Task<List<MeetingHistoryDto>> HistoryAsync(int maxCount = 20)
        => _service.GetHistoryAsync(maxCount);

    /// <summary>解析晨会议程计划文本</summary>
    /// <param name="input">计划文本输入</param>
    /// <returns>解析出的结构化议程计划</returns>
    [HttpPost("parse-plan")]
    public Task<PlanParseResult> ParsePlanAsync([FromBody] PlanParseInput input)
        => _service.ParsePlanAsync(input.PlanText);

    /// <summary>按 ID 获取会议记录</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>会议记录详情</returns>
    [HttpGet("{id}")]
    public Task<MeetingRecordDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>生成晨会稿草稿</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>生成的晨会稿草稿</returns>
    [HttpPost("{id}/speech/generate")]
    public Task<SpeechDraftDto> GenerateSpeechAsync(Guid id)
        => _service.GenerateSpeechAsync(id);

    /// <summary>
    /// 流式生成晨会稿：text/plain 逐段推送 LLM 增量文本。
    /// 请求正常结束即代表已落库；中途断开/报错则前端按失败处理，可重试。
    /// 走 ISpeechDraftStreamer（普通服务，不经 ABP 校验/审计拦截器序列化参数）。
    /// </summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>无返回值；响应体为逐段推送的增量文本流</returns>
    [HttpPost("{id}/speech/generate/stream")]
    public async Task GenerateSpeechStreamAsync(Guid id)
    {
        var ct = HttpContext.RequestAborted;
        Response.ContentType = "text/plain; charset=utf-8";
        Response.Headers["X-Accel-Buffering"] = "no";
        try
        {
            await _streamer.GenerateStreamAsync(
                id,
                async (delta, token) =>
                {
                    await Response.WriteAsync(delta, token);
                    await Response.Body.FlushAsync(token);
                },
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 客户端已断开：静默结束
        }
        catch (Exception)
        {
            // 尚未写任何内容时返回 500（text/plain 无 output formatter，
            // 直接抛会变 406）；已开始推送则响应正常结束（部分文本已展示）
            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Response.WriteAsync("晨会稿生成失败，请重试");
            }
        }
    }

    /// <summary>获取已生成的晨会稿草稿</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>晨会稿草稿；尚未生成时为 null</returns>
    [HttpGet("{id}/speech")]
    public Task<SpeechDraftDto?> GetSpeechAsync(Guid id)
        => _service.GetSpeechAsync(id);

    /// <summary>更新晨会稿草稿内容</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="input">晨会稿更新输入</param>
    /// <returns>更新后的晨会稿草稿</returns>
    [HttpPut("{id}/speech")]
    public Task<SpeechDraftDto> UpdateSpeechAsync(Guid id, [FromBody] UpdateSpeechInput input)
        => _service.UpdateSpeechAsync(id, input.Content);

    /// <summary>获取晨会稿合成音频</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>WAV 格式音频文件</returns>
    [HttpGet("{id}/speech/audio")]
    public async Task<IActionResult> SpeechAudioAsync(Guid id)
    {
        var audio = await _service.GetSpeechAudioAsync(id);
        return File(audio, "audio/wav");
    }

    /// <summary>查询晨会稿音频缓存状态</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>音频缓存状态，含导语缓存与导语文本</returns>
    [HttpGet("{id}/speech/audio/status")]
    public async Task<MeetingBot.SpeechAudioStatusDto> SpeechAudioStatusAsync(Guid id)
    {
        return new MeetingBot.SpeechAudioStatusDto
        {
            Cached = await _service.IsSpeechAudioCachedAsync(id),
            LeadCached = await _service.IsSpeechLeadAudioCachedAsync(id),
            LeadText = await _service.GetSpeechLeadTextAsync(id)
        };
    }

    /// <summary>获取晨会稿导语音频</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>WAV 格式导语音频；不存在时返回 404</returns>
    [HttpGet("{id}/speech/audio/lead")]
    public async Task<IActionResult> SpeechLeadAudioAsync(Guid id)
    {
        var audio = await _service.GetSpeechLeadAudioAsync(id);
        return audio is null ? NotFound() : File(audio, "audio/wav");
    }

    /// <summary>获取晨会稿指定段落音频</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="index">段落序号，从 0 开始</param>
    /// <returns>WAV 格式段落音频；不存在时返回 404</returns>
    [HttpGet("{id}/speech/audio/segment/{index}")]
    public async Task<IActionResult> SpeechSegmentAudioAsync(Guid id, int index)
    {
        var audio = await _service.GetSpeechSegmentAudioAsync(id, index);
        return audio is null ? NotFound() : File(audio, "audio/wav");
    }

    /// <summary>上传并缓存整段合成音频</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="file">WAV 格式音频文件</param>
    [HttpPost("{id}/speech/audio/cache")]
    public async Task SaveSpeechAudioCacheAsync(Guid id, IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        await _service.SaveSpeechAudioCacheAsync(id, ms.ToArray());
    }

    /// <summary>开始会议流程</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>更新后的会议记录</returns>
    [HttpPost("{id}/start")]
    public Task<MeetingRecordDto> StartAsync(Guid id)
        => _service.StartAsync(id);

    /// <summary>识别参会合影中的出勤人脸并统计到场人数</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="image">参会合影图片</param>
    /// <returns>识别出的人脸列表与人头总数</returns>
    [HttpPost("{id}/attendance/recognize")]
    public async Task<AttendanceRecognizeResult> RecognizeAsync(Guid id, IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var faces = await _service.RecognizeAttendanceAsync(id, ms.ToArray());
        var count = await _bot.CountAsync(ms.ToArray());
        return new AttendanceRecognizeResult { Faces = faces, Count = count };
    }

    /// <summary>保存未识别出的现场人脸，供后续补录</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="files">未识别人脸图片列表</param>
    /// <param name="metadata">可选 JSON 元数据（每张图对应的 confidence/bbox）</param>
    /// <returns>成功保存的人脸数量</returns>
    [HttpPost("{id}/unrecognized-faces")]
    public async Task<int> SaveUnrecognizedFacesAsync(Guid id, List<IFormFile> files, [FromForm] string? metadata)
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

    /// <summary>获取会议出勤记录</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>出勤人脸项列表</returns>
    [HttpGet("{id}/attendance")]
    public Task<List<AttendanceItemDto>> AttendanceAsync(Guid id)
        => _service.GetAttendanceAsync(id);

    /// <summary>基于会议内容提问</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="input">问题输入</param>
    /// <returns>生成的问答记录</returns>
    [HttpPost("{id}/qa")]
    public Task<QaRecordDto> AskQaAsync(Guid id, [FromBody] AskQaInput input)
        => _service.AskQaAsync(id, input.Question);

    /// <summary>语音提问：先语音识别再回答问题</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="audio">提问语音文件</param>
    /// <returns>生成的问答记录</returns>
    [HttpPost("{id}/qa/audio")]
    public async Task<QaRecordDto> AskQaAudioAsync(Guid id, IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var text = await _bot.AsrAsync(ms.ToArray());
        return await _service.AskQaAsync(id, text);
    }

    /// <summary>获取问答回复的语音音频</summary>
    /// <param name="qaId">问答记录 ID</param>
    /// <returns>WAV 格式音频文件</returns>
    [HttpGet("qa/{qaId}/audio")]
    public async Task<IActionResult> QaAudioAsync(Guid qaId)
    {
        var audio = await _service.GetQaAudioAsync(qaId);
        return File(audio, "audio/wav");
    }

    /// <summary>语音识别：将音频转为文本</summary>
    /// <param name="audio">音频文件</param>
    /// <returns>识别出的文本</returns>
    [HttpPost("asr")]
    public async Task<string> AsrAsync(IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        return await _bot.AsrAsync(ms.ToArray());
    }

    /// <summary>文本转语音合成</summary>
    /// <param name="input">待合成文本；空文本返回 400</param>
    /// <returns>WAV 格式音频文件</returns>
    [HttpPost("tts")]
    public async Task<IActionResult> TtsAsync([FromBody] TtsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return BadRequest();
        }
        var audio = await _bot.TtsAsync(input.Text);
        return File(audio, "audio/wav");
    }

    /// <summary>流式文本转语音：零加工直通输出上游原始 PCM 流；上游中断时中止响应</summary>
    /// <param name="input">待合成文本；空文本返回 400</param>
    [HttpPost("tts/stream")]
    public async Task TtsStreamAsync([FromBody] TtsInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        Response.ContentType = "application/octet-stream";
        // 输出 DGX Qwen3 原始 PCM 流（23040Hz/16bit/单声道），零加工直通（53fa 同款）；
        // 上游中断时异常冒泡中止响应，前端据此感知不完整并回退逐段合成
        Response.Headers["x-sample-rate"] = "23040";
        await _bot.StreamTtsAsync(input.Text, Response.Body, HttpContext.RequestAborted);
    }

    /// <summary>保存会议录音音频</summary>
    /// <param name="id">会议记录 ID</param>
    /// <param name="audio">录音音频文件</param>
    /// <returns>更新后的会议记录</returns>
    [HttpPost("{id}/recording")]
    public async Task<MeetingRecordDto> SaveRecordingAsync(Guid id, IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        return await _service.SaveRecordingAsync(id, ms.ToArray(), audio.FileName);
    }

    /// <summary>完成会议流程</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>更新后的会议记录</returns>
    [HttpPost("{id}/complete")]
    public Task<MeetingRecordDto> CompleteAsync(Guid id)
        => _service.CompleteAsync(id);

    /// <summary>获取会议报告</summary>
    /// <param name="id">会议记录 ID</param>
    /// <returns>会议报告；尚未生成时为 null</returns>
    [HttpGet("{id}/report")]
    public Task<ReportDto?> ReportAsync(Guid id)
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

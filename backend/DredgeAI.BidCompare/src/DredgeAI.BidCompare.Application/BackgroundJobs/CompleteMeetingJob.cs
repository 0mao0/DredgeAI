using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 会后任务：全程录音转写（meeting-bot /transcribe）→ 生成 Markdown 报告 → 落盘。
/// 失败重试 2 次，仍失败记录错误状态。
/// </summary>
public class CompleteMeetingJob : AsyncBackgroundJob<CompleteMeetingArgs>, ITransientDependency
{
    private const int MaxAttempts = 3;

    private readonly IRepository<MeetingRecord, Guid> _meetings;
    private readonly IRepository<AttendanceRecord, Guid> _attendance;
    private readonly IRepository<QaRecord, Guid> _qa;
    private readonly IMeetingBotClient _bot;
    private readonly IFileStorage _fileStorage;

    public CompleteMeetingJob(
        IRepository<MeetingRecord, Guid> meetings,
        IRepository<AttendanceRecord, Guid> attendance,
        IRepository<QaRecord, Guid> qa,
        IMeetingBotClient bot,
        IFileStorage fileStorage)
    {
        _meetings = meetings;
        _attendance = attendance;
        _qa = qa;
        _bot = bot;
        _fileStorage = fileStorage;
    }

    public override async Task ExecuteAsync(CompleteMeetingArgs args)
    {
        var meeting = await _meetings.GetAsync(args.MeetingRecordId);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await ExecuteCoreAsync(meeting);
                meeting.SetReport($"meeting/{meeting.Id}/report.md", error: null);
                await _meetings.UpdateAsync(meeting);
                Logger.LogInformation("晨会 {Id} 报告生成完成", meeting.Id);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                Logger.LogWarning(ex, "晨会报告生成第 {Attempt}/{Max} 次失败，重试中", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "晨会报告生成最终失败 {Id}", meeting.Id);
                meeting.SetReport(null, ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000]);
                await _meetings.UpdateAsync(meeting);
            }
        }
    }

    private async Task ExecuteCoreAsync(MeetingRecord meeting)
    {
        if (string.IsNullOrEmpty(meeting.TranscriptFile))
        {
            meeting.SetTranscript(null);
            await _meetings.UpdateAsync(meeting);
        }
        else if (string.IsNullOrEmpty(meeting.TranscriptText))
        {
            byte[] audio;
            await using (var stream = await _fileStorage.GetAsync(meeting.TranscriptFile))
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                audio = ms.ToArray();
            }
            var transcript = await _bot.TranscribeAsync(audio, CancellationToken.None);
            meeting.SetTranscript(transcript);
            await _meetings.UpdateAsync(meeting);
        }

        var markdown = await BuildReportMarkdownAsync(meeting);
        var key = $"meeting/{meeting.Id}/report.md";
        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes(markdown)))
        {
            await _fileStorage.UploadAsync(key, content, "text/markdown");
        }
        meeting.SetReport(key);
    }

    private async Task<string> BuildReportMarkdownAsync(MeetingRecord meeting)
    {
        var attendance = await _attendance.GetListAsync(a => a.MeetingRecordId == meeting.Id);
        var qaRecords = await _qa.GetListAsync(q => q.MeetingRecordId == meeting.Id);

        var sb = new StringBuilder();
        sb.AppendLine($"# AI 晨会报告");
        sb.AppendLine();
        sb.AppendLine($"- 会议时间：{meeting.Date:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- 状态：完成");
        sb.AppendLine();
        sb.AppendLine("## 出勤情况");
        sb.AppendLine();
        sb.AppendLine("| 姓名 | 班组 | 状态 | 置信度 |");
        sb.AppendLine("|------|------|------|--------|");
        foreach (var a in attendance.OrderBy(a => a.CreationTime))
        {
            sb.AppendLine($"| {a.Name} | {a.Team} | {a.Status} | {a.Confidence:P0} |");
        }

        sb.AppendLine();
        sb.AppendLine("## 会议转写");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(meeting.TranscriptText)
            ? "（未提供会议录音或转写尚未完成）"
            : meeting.TranscriptText);

        sb.AppendLine();
        sb.AppendLine("## 问答记录");
        sb.AppendLine();
        if (qaRecords.Count == 0)
        {
            sb.AppendLine("（无问答记录）");
        }
        foreach (var q in qaRecords.OrderBy(q => q.CreationTime))
        {
            sb.AppendLine($"### Q：{q.Question}");
            sb.AppendLine();
            sb.AppendLine(q.Answer);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

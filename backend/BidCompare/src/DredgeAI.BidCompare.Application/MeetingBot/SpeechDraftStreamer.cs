using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>
/// 晨会稿生成编排实现（供 MeetingRecordAppService 与 HttpApi 流式端点共用）。
/// 注意：本类为普通 ITransientDependency，不挂 ABP 拦截器 —— ApplicationService 的
/// 校验/审计拦截器会序列化方法参数，Func 回调会导致 System.Text.Json 序列化失败，
/// 因此流式回调参数必须放在本类而非 AppService 方法签名中。
/// </summary>
public class SpeechDraftStreamer : ISpeechDraftStreamer, ITransientDependency
{
    private const string SpeechSystemPrompt =
        "你是工地晨会主持人助手。请根据用户提供的前置信息（日期/天气/今日任务/风险点）与知识库证据，" +
        "生成一段可直接朗读的晨会稿，结构为：开场问候 → 今日任务 → 安全交底（结合风险点）→ 结束语。" +
        "语气口语化、面向现场工人，总字数 300-500 字。若知识库证据为空，在结尾注明“本段依据无知识库证据”。";

    private const string SpeechAudioCachePrefix = "meeting/speech";

    private readonly IRepository<MeetingRecord, Guid> _meetings;
    private readonly IRepository<SpeechDraft, Guid> _drafts;
    private readonly IAnGineerClient _anGineer;
    private readonly ILlmGateway _llmGateway;
    private readonly IFileStorage _fileStorage;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<SpeechDraftStreamer> _logger;

    public SpeechDraftStreamer(
        IRepository<MeetingRecord, Guid> meetings,
        IRepository<SpeechDraft, Guid> drafts,
        IAnGineerClient anGineer,
        ILlmGateway llmGateway,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager,
        IGuidGenerator guidGenerator,
        ILogger<SpeechDraftStreamer> logger)
    {
        _meetings = meetings;
        _drafts = drafts;
        _anGineer = anGineer;
        _llmGateway = llmGateway;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var (meeting, userPrompt) = await BuildAsync(id, cancellationToken);
        var content = await _llmGateway.CompleteAsync(SpeechSystemPrompt, userPrompt, cancellationToken);
        await PersistAsync(meeting, content);
        return content;
    }

    public async Task<string> GenerateStreamAsync(
        Guid id,
        Func<string, CancellationToken, Task> onDelta,
        CancellationToken cancellationToken = default)
    {
        var (meeting, userPrompt) = await BuildAsync(id, cancellationToken);
        var builder = new StringBuilder();
        try
        {
            await foreach (var delta in _llmGateway.CompleteStreamAsync(SpeechSystemPrompt, userPrompt, cancellationToken))
            {
                builder.Append(delta);
                await onDelta(delta, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端主动取消：不保存、不吞掉
            throw;
        }
        catch (Exception ex)
        {
            // LLM 流上游中断：已推送的增量保留在页面上，已生成部分照常落库，
            // 避免把异常冒泡到已开始写 body 的流式响应（会变成 406）
            _logger.LogWarning(ex, "晨会稿 LLM 流式中断，保存已生成部分（{Length} 字）", builder.Length);
        }
        var content = builder.ToString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessException("SPEECH_DRAFT_GENERATE_FAILED", "晨会稿生成失败，请重试");
        }
        await PersistAsync(meeting, content);
        return content;
    }

    private async Task<(MeetingRecord Meeting, string UserPrompt)> BuildAsync(Guid id, CancellationToken ct)
    {
        var meeting = await _meetings.GetAsync(id, cancellationToken: ct);
        var preInfo = ParsePreInfo(meeting.PreInfoJson);

        var query = $"晨会安全交底、今日任务：{preInfo.Tasks}；风险点：{preInfo.RiskPoints}" +
            (string.IsNullOrWhiteSpace(preInfo.ProjectName) ? "" : $"；项目：{preInfo.ProjectName}");
        IReadOnlyList<AnGineerHit> hits;
        try
        {
            hits = await _anGineer.SearchAsync(query, topK: 5);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "晨会稿检索失败，降级为纯 LLM 生成");
            hits = [];
        }

        var evidence = hits.Count > 0
            ? string.Join("\n", hits.Select(h => $"- [{h.Title}]({h.DocId}) {h.Text}"))
            : "（无知识库证据）";

        var projectContext = string.IsNullOrWhiteSpace(preInfo.ProjectName)
            ? ""
            : $"当前项目：{preInfo.ProjectName}。" +
              (string.IsNullOrWhiteSpace(preInfo.ProjectSummary)
                  ? ""
                  : $"施工方案要点：{preInfo.ProjectSummary}。") +
              "\n";
        var userPrompt =
            $"前置信息：日期 {preInfo.Date:yyyy-MM-dd}，天气 {preInfo.Weather}，" +
            $"今日任务 {preInfo.Tasks}，风险点 {preInfo.RiskPoints}。\n\n" +
            projectContext +
            $"<evidence>\n{evidence}\n</evidence>\n\n请生成晨会稿。";

        return (meeting, userPrompt);
    }

    private async Task PersistAsync(MeetingRecord meeting, string content)
    {
        SpeechDraft draft;
        if (meeting.SpeechDraftId is null)
        {
            draft = new SpeechDraft(_guidGenerator.Create(), meeting.Id, content);
            await _drafts.InsertAsync(draft);
            meeting.AttachSpeechDraft(draft.Id);
            meeting.MarkPrepared();
            await _meetings.UpdateAsync(meeting);
            await InvalidateSpeechAudioCacheAsync(meeting.Id);
            await _backgroundJobManager.EnqueueAsync(new WarmSpeechAudioArgs { MeetingRecordId = meeting.Id });
        }
        else
        {
            draft = await _drafts.GetAsync(meeting.SpeechDraftId.Value);
            draft.SetContent(content);
            await _drafts.UpdateAsync(draft);
            meeting.MarkPrepared();
            await _meetings.UpdateAsync(meeting);
            await InvalidateSpeechAudioCacheAsync(meeting.Id);
            await _backgroundJobManager.EnqueueAsync(new WarmSpeechAudioArgs { MeetingRecordId = meeting.Id });
        }
    }

    private async Task InvalidateSpeechAudioCacheAsync(Guid meetingId)
    {
        try
        {
            await _fileStorage.DeleteAsync($"{SpeechAudioCachePrefix}/{meetingId}.wav");
            await _fileStorage.DeleteAsync($"{SpeechAudioCachePrefix}/{meetingId}/lead.wav");
            await _fileStorage.DeleteByPrefixAsync($"{SpeechAudioCachePrefix}/{meetingId}/seg/");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理晨会稿语音缓存失败（{MeetingId}）", meetingId);
        }
    }

    internal static PreInfoSnapshot ParsePreInfo(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PreInfoSnapshot>(json) ?? new PreInfoSnapshot();
        }
        catch (JsonException)
        {
            return new PreInfoSnapshot();
        }
    }

    internal class PreInfoSnapshot
    {
        public DateTime Date { get; set; }

        public string Weather { get; set; } = "";

        public string Tasks { get; set; } = "";

        public string RiskPoints { get; set; } = "";

        public string ProjectName { get; set; } = "";

        public string ProjectSummary { get; set; } = "";
    }
}

using System;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DredgeAI.BidCompare.MeetingBot;

public class WarmSpeechAudioArgs
{
    public Guid MeetingRecordId { get; set; }
}

/// <summary>晨会稿生成后后台预合成整段语音（写缓存），用户打开晨会稿页时通常已就绪。</summary>
public class WarmSpeechAudioJob : AsyncBackgroundJob<WarmSpeechAudioArgs>, ITransientDependency
{
    private readonly IMeetingRecordAppService _service;
    private readonly ILogger<WarmSpeechAudioJob> _logger;

    public WarmSpeechAudioJob(IMeetingRecordAppService service, ILogger<WarmSpeechAudioJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public override async Task ExecuteAsync(WarmSpeechAudioArgs args)
    {
        try
        {
            // 只预热开场句：用户点播放第一句能秒出；
            // 后续由前端“流式合成”一次请求按句生成，避免后台任务与实时流抢占同一个 TTS worker
            await _service.PreWarmSpeechLeadAsync(args.MeetingRecordId);
        }
        catch (Exception ex)
        {
            // 合成失败不影响晨会稿主流程，用户仍可在页面手动重试
            _logger.LogWarning(ex, "晨会稿语音预合成失败（{MeetingId}）", args.MeetingRecordId);
        }
    }
}

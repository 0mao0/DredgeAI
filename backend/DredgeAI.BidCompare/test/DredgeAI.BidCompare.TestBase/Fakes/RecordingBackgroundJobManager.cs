using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;

namespace DredgeAI.BidCompare;

/// <summary>记录入队参数而不真正执行；测试手动解析 Job 类并调用 ExecuteAsync，保证确定性。</summary>
public class RecordingBackgroundJobManager : IBackgroundJobManager
{
    public List<object> EnqueuedArgs { get; } = new();

    public Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        BackgroundJobPriority priority = BackgroundJobPriority.Normal,
        TimeSpan? delay = null)
    {
        EnqueuedArgs.Add(args!);
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    public void Clear() => EnqueuedArgs.Clear();

    public TArgs? LastEnqueued<TArgs>()
    {
        for (var i = EnqueuedArgs.Count - 1; i >= 0; i--)
        {
            if (EnqueuedArgs[i] is TArgs typed)
            {
                return typed;
            }
        }
        return default;
    }
}

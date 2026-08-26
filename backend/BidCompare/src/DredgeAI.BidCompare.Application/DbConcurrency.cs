using System;
using Volo.Abp;

namespace DredgeAI.BidCompare;

/// <summary>
/// 乐观并发冲突识别与翻译。AbpDbConcurrencyException 定义于 Volo.Abp.EntityFrameworkCore，
/// Application 层按 DDD 分层不引用该包，按类型名识别。
/// </summary>
internal static class DbConcurrency
{
    public static bool IsConflict(Exception ex)
        => ex.GetType().Name is "AbpDbConcurrencyException" or "DbUpdateConcurrencyException";

    /// <summary>状态迁移冲突翻译为业务异常（前端提示刷新重试，而不是 500）。</summary>
    public static BusinessException ToInvalidState(string action)
        => new BusinessException(BidCompareErrorCodes.InvalidTaskState)
            .WithData("action", action)
            .WithData("reason", "任务状态已被其他操作变更，请刷新后重试");
}

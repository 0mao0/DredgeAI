using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Applications;

/// <summary>
/// 应用展示顺序，按 <see cref="Level"/> 分级——Global 全局默认（admin 维护，含主应用全局顺序与子应用同母项组内顺序），
/// User 用户个性化；替代 app-order.json 的全部功能。
/// </summary>
public class AppOrder : FullAuditedEntity<Guid>
{
    /// <summary>排序级别：Global=全局默认；User=用户个性化。</summary>
    public AppOrderLevel Level { get; private set; }

    /// <summary>用户 id；Level=Global 时固定为 Guid.Empty。</summary>
    public Guid UserId { get; private set; }

    /// <summary>排序目标：Global=应用目录条目 id（uuid 文本）；User=用户端应用路由（如 "/ai-bid"）。</summary>
    public string TargetId { get; private set; } = default!;

    /// <summary>顺序（小在前）。Global：主应用全局 / 子应用同母项组内；User：用户维度。</summary>
    public int SortOrder { get; private set; }

    protected AppOrder()
    {
    }

    public AppOrder(Guid id, AppOrderLevel level, Guid userId, string targetId, int sortOrder) : base(id)
    {
        if (level == AppOrderLevel.Global && userId != Guid.Empty)
        {
            throw new ArgumentException("全局排序行的用户 id 必须为 Guid.Empty", nameof(userId));
        }
        Level = level;
        UserId = userId;
        TargetId = Check.NotNullOrWhiteSpace(targetId, nameof(targetId), maxLength: 128);
        SortOrder = sortOrder;
    }

    /// <summary>与相邻条目交换顺序时使用。</summary>
    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}

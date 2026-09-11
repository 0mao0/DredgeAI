using System;

namespace DredgeAI.BidCompare.Applications;

/// <summary>user-web 应用卡片（由目录按发布状态推导）。</summary>
public class UserAppCardDto
{
    public Guid Id { get; set; }

    public Guid? ParentAppId { get; set; }

    public string Title { get; set; } = default!;

    public string Description { get; set; } = default!;

    /// <summary>应用分类（枚举 wire 值为 snake_case）。</summary>
    public AppCatalogCategory Category { get; set; }

    public string Icon { get; set; } = default!;

    /// <summary>卡片派生展示串（已授权 / 已下架），非实体枚举。</summary>
    public string Status { get; set; } = "已授权";

    public string Route { get; set; } = default!;

    public string? Version { get; set; }

    public bool Pinned { get; set; }
}

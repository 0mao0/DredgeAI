using System;

namespace DredgeAI.BidCompare.Applications;

/// <summary>admin 发布管理下的子应用。</summary>
public class AppCatalogSubAppDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>应用分类（枚举 wire 值为 snake_case）。</summary>
    public AppCatalogCategory Category { get; set; }

    public Guid ParentAppId { get; set; }

    public string? Route { get; set; }

    public string Icon { get; set; } = default!;

    public string Version { get; set; } = default!;

    /// <summary>状态：published/unpublished。</summary>
    public AppCatalogStatus Status { get; set; }

    /// <summary>授权范围（默认 public）。</summary>
    public AppCatalogScope Scope { get; set; }

    public string? Description { get; set; }
}

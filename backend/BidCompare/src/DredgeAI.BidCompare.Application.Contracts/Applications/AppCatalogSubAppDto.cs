using System;
using System.Collections.Generic;

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

    /// <summary>拥有该子应用 View 权限的角色名列表（仅角色 R 授权，去重排序；空数组=未授权任何角色）。</summary>
    public List<string> GrantedRoles { get; set; } = [];
}

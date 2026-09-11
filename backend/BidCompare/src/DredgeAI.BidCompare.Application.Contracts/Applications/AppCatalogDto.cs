using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.Applications;

/// <summary>应用目录条目（admin 发布管理 / user 端应用列表共用同一份数据）。</summary>
public class AppCatalogDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>应用分类（枚举 wire 值为 snake_case，如 general/operation/design/construction）。</summary>
    public AppCatalogCategory Category { get; set; }

    public string? Manager { get; set; }

    public string Version { get; set; } = default!;

    /// <summary>状态：主应用 online/offline，子应用 published/unpublished。</summary>
    public AppCatalogStatus Status { get; set; }

    public int? UserCount { get; set; }

    public int? ApiCalls { get; set; }

    /// <summary>创建日期（yyyy-MM-dd，由审计字段 CreationTime 格式化）。</summary>
    public string CreatedAt { get; set; } = default!;

    public string Icon { get; set; } = default!;

    /// <summary>admin 侧路由（可选）。</summary>
    public string? Route { get; set; }

    /// <summary>user-web 侧边栏路由（仅无子应用的主应用使用；子应用用 sub.Route）。</summary>
    public string? UserRoute { get; set; }

    /// <summary>授权范围（默认 public）。</summary>
    public AppCatalogScope Scope { get; set; }

    public List<AppCatalogSubAppDto>? SubApps { get; set; }
}

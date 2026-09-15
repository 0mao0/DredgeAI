using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Applications;

/// <summary>
/// 应用目录条目；<see cref="ParentAppId"/> 为空为主应用，否则为子应用。
/// 创建时间用审计字段 CreationTime，不单设列；展示顺序不在本实体，由 <see cref="AppOrder"/> 全局行承担。
/// </summary>
public class AppCatalog : FullAuditedAggregateRoot<Guid>
{
    /// <summary>所属主应用 id；null = 主应用。</summary>
    public Guid? ParentAppId { get; private set; }

    /// <summary>应用名称。</summary>
    public string Name { get; private set; } = default!;

    /// <summary>应用分类。</summary>
    public AppCatalogCategory Category { get; private set; }

    /// <summary>antd 图标名。</summary>
    public string Icon { get; private set; } = default!;

    /// <summary>版本号（如 v2.1.0）。</summary>
    public string Version { get; private set; } = default!;

    /// <summary>状态：主应用 Online/Offline；子应用 Published/Unpublished。</summary>
    public AppCatalogStatus Status { get; private set; }

    /// <summary>路由：主应用为 admin 侧路由，子应用为 user-web 路由（可空）。</summary>
    public string? Route { get; private set; }

    /// <summary>授权范围（默认 Public）。</summary>
    public AppCatalogScope Scope { get; private set; } = AppCatalogScope.Public;

    /// <summary>负责人（仅主应用）。</summary>
    public string? Manager { get; private set; }

    /// <summary>使用人数（仅主应用，展示指标）。</summary>
    public int? UserCount { get; private set; }

    /// <summary>API 调用量（仅主应用，展示指标）。</summary>
    public int? ApiCalls { get; private set; }

    /// <summary>user-web 侧边栏路由（仅无子应用的主应用）。</summary>
    public string? UserRoute { get; private set; }

    /// <summary>子应用描述（仅子应用）。</summary>
    public string? Description { get; private set; }

    protected AppCatalog()
    {
    }

    /// <summary>创建主应用（status 仅允许 Online/Offline，否则 ArgumentException）。</summary>
    public static AppCatalog CreateMain(Guid id, string name, AppCatalogCategory category, string icon, string version,
        AppCatalogStatus status, string? route, AppCatalogScope scope,
        string? manager, int? userCount, int? apiCalls, string? userRoute)
    {
        if (status is not (AppCatalogStatus.Online or AppCatalogStatus.Offline))
        {
            throw new ArgumentException("主应用状态仅允许 Online/Offline", nameof(status));
        }
        return new AppCatalog
        {
            Id = id,
            ParentAppId = null,
            Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 64),
            Category = category,
            Icon = Check.NotNullOrWhiteSpace(icon, nameof(icon), maxLength: 64),
            Version = Check.NotNullOrWhiteSpace(version, nameof(version), maxLength: 32),
            Status = status,
            Route = route,
            Scope = scope,
            Manager = manager,
            UserCount = userCount,
            ApiCalls = apiCalls,
            UserRoute = userRoute,
            Description = null
        };
    }

    /// <summary>创建子应用（status 仅允许 Published/Unpublished；parentAppId 必填）。</summary>
    public static AppCatalog CreateSub(Guid id, Guid parentAppId, string name,
        AppCatalogCategory category, string icon, string version, AppCatalogStatus status, string? route,
        AppCatalogScope scope, string? description)
    {
        if (status is not (AppCatalogStatus.Published or AppCatalogStatus.Unpublished))
        {
            throw new ArgumentException("子应用状态仅允许 Published/Unpublished", nameof(status));
        }
        if (parentAppId == Guid.Empty)
        {
            throw new ArgumentException("子应用必须有所属主应用 id", nameof(parentAppId));
        }
        return new AppCatalog
        {
            Id = id,
            ParentAppId = parentAppId,
            Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 64),
            Category = category,
            Icon = Check.NotNullOrWhiteSpace(icon, nameof(icon), maxLength: 64),
            Version = Check.NotNullOrWhiteSpace(version, nameof(version), maxLength: 32),
            Status = status,
            Route = route,
            Scope = scope,
            Manager = null,
            UserCount = null,
            ApiCalls = null,
            UserRoute = null,
            Description = description
        };
    }

    /// <summary>设置状态：主应用仅 Online/Offline、子应用仅 Published/Unpublished，否则 ArgumentException。</summary>
    public void SetStatus(AppCatalogStatus status)
    {
        if (ParentAppId == null && status is not (AppCatalogStatus.Online or AppCatalogStatus.Offline))
        {
            throw new ArgumentException("主应用状态仅允许 Online/Offline", nameof(status));
        }
        if (ParentAppId != null && status is not (AppCatalogStatus.Published or AppCatalogStatus.Unpublished))
        {
            throw new ArgumentException("子应用状态仅允许 Published/Unpublished", nameof(status));
        }
        Status = status;
    }

    /// <summary>设置分类。</summary>
    public void SetCategory(AppCatalogCategory category)
    {
        Category = category;
    }

    /// <summary>设置 antd 图标名。</summary>
    public void SetIcon(string icon)
    {
        Icon = Check.NotNullOrWhiteSpace(icon, nameof(icon), maxLength: 64);
    }
}

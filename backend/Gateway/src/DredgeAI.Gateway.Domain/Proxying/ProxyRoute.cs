using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

/// <summary>YARP 代理路由（入库管理；ConfigJson 存整份 RouteConfig JSON，运行时由 DatabaseProxyConfigProvider 直通为 YARP 快照）。</summary>
public class ProxyRoute : FullAuditedAggregateRoot<Guid>
{
    /// <summary>YARP 路由 ID，全局唯一（从 Config 同步冗余，供唯一索引/查询）。</summary>
    public string RouteId { get; private set; } = default!;

    /// <summary>引用的集群 ID（从 Config 同步冗余，供查询）。</summary>
    public string ClusterId { get; private set; } = default!;

    /// <summary>路由匹配优先级（值越小越优先；仅用于列表排序，原始 null 语义保留在 ConfigJson 内）。</summary>
    public int Order { get; private set; }

    /// <summary>是否启用；禁用的路由不进 YARP 快照。</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>完整 YARP RouteConfig JSON（camelCase）。</summary>
    public string ConfigJson { get; private set; } = default!;

    /// <summary>路由描述（管理端元数据，YARP 无此概念）。</summary>
    public string? Description { get; private set; }

    protected ProxyRoute()
    {
    }

    public ProxyRoute(Guid id, RouteConfig config, string? description = null) : base(id)
    {
        SetConfig(config);
        Description = description;
        IsEnabled = true;
    }

    public void Update(RouteConfig config, string? description = null)
    {
        SetConfig(config);
        Description = description;
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    public RouteConfig ToRouteConfig()
    {
        return ProxyConfigJson.DeserializeRoute(ConfigJson);
    }

    private void SetConfig(RouteConfig config)
    {
        Check.NotNull(config, nameof(config));
        RouteId = Check.NotNullOrWhiteSpace(config.RouteId, nameof(config.RouteId), maxLength: 128);
        ClusterId = Check.NotNullOrWhiteSpace(config.ClusterId, nameof(config.ClusterId), maxLength: 128);
        Order = config.Order ?? 0;
        ConfigJson = ProxyConfigJson.SerializeRoute(config);
    }
}

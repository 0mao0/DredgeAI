using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.Gateway.Proxying;

/// <summary>YARP 代理路由（入库管理，运行时由 DatabaseProxyConfigProvider 转为 RouteConfig 快照）。</summary>
public class ProxyRoute : FullAuditedAggregateRoot<Guid>
{
    /// <summary>YARP 路由 ID，全局唯一。</summary>
    public string RouteId { get; private set; } = default!;

    /// <summary>引用的集群 ID。</summary>
    public string ClusterId { get; private set; } = default!;

    /// <summary>路由匹配优先级（值越小越优先）。</summary>
    public int Order { get; private set; }

    /// <summary>路径匹配模式，如 /api/compare/{**catch-all}。</summary>
    public string MatchPath { get; private set; } = default!;

    /// <summary>Host 匹配列表（JSON 数组），null 表示不限制。</summary>
    public string? MatchHostsJson { get; private set; }

    /// <summary>HTTP 方法匹配列表（JSON 数组），null 表示不限制。</summary>
    public string? MatchMethodsJson { get; private set; }

    /// <summary>授权策略（YARP 内置字面量 "anonymous" 或 "default"）。</summary>
    public string AuthorizationPolicy { get; private set; } = default!;

    /// <summary>是否启用；禁用的路由不进 YARP 快照。</summary>
    public bool IsEnabled { get; private set; }

    protected ProxyRoute()
    {
    }

    public ProxyRoute(
        Guid id,
        string routeId,
        string clusterId,
        int order,
        string matchPath,
        string? matchHostsJson,
        string? matchMethodsJson,
        string authorizationPolicy) : base(id)
    {
        SetValues(routeId, clusterId, order, matchPath, matchHostsJson, matchMethodsJson, authorizationPolicy);
        IsEnabled = true;
    }

    public void Update(
        string routeId,
        string clusterId,
        int order,
        string matchPath,
        string? matchHostsJson,
        string? matchMethodsJson,
        string authorizationPolicy)
    {
        SetValues(routeId, clusterId, order, matchPath, matchHostsJson, matchMethodsJson, authorizationPolicy);
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    private void SetValues(
        string routeId,
        string clusterId,
        int order,
        string matchPath,
        string? matchHostsJson,
        string? matchMethodsJson,
        string authorizationPolicy)
    {
        RouteId = Check.NotNullOrWhiteSpace(routeId, nameof(routeId), maxLength: 128);
        ClusterId = Check.NotNullOrWhiteSpace(clusterId, nameof(clusterId), maxLength: 128);
        Order = order;
        MatchPath = Check.NotNullOrWhiteSpace(matchPath, nameof(matchPath), maxLength: 256);
        MatchHostsJson = matchHostsJson;
        MatchMethodsJson = matchMethodsJson;
        AuthorizationPolicy = Check.NotNullOrWhiteSpace(authorizationPolicy, nameof(authorizationPolicy), maxLength: 64);
    }
}

using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

/// <summary>YARP 代理集群（ConfigJson 存整份 ClusterConfig JSON，含 Destinations/HealthCheck/SessionAffinity 等全部能力）。</summary>
public class ProxyCluster : FullAuditedAggregateRoot<Guid>
{
    /// <summary>YARP 集群 ID，全局唯一。</summary>
    public string ClusterId { get; private set; } = default!;

    /// <summary>完整 YARP ClusterConfig JSON（camelCase）。</summary>
    public string ConfigJson { get; private set; } = default!;

    /// <summary>集群描述（管理端元数据，YARP 无此概念）。</summary>
    public string? Description { get; private set; }

    /// <summary>是否启用；禁用的集群及其路由不进 YARP 快照。</summary>
    public bool IsEnabled { get; private set; }

    protected ProxyCluster()
    {
    }

    public ProxyCluster(Guid id, ClusterConfig config, string? description = null) : base(id)
    {
        ClusterId = Check.NotNullOrWhiteSpace(config.ClusterId, nameof(config.ClusterId), maxLength: 128);
        ConfigJson = ProxyConfigJson.SerializeCluster(config);
        Description = description;
        IsEnabled = true;
    }

    public void Update(ClusterConfig config, string? description = null)
    {
        ClusterId = Check.NotNullOrWhiteSpace(config.ClusterId, nameof(config.ClusterId), maxLength: 128);
        ConfigJson = ProxyConfigJson.SerializeCluster(config);
        Description = description;
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public ClusterConfig ToClusterConfig()
    {
        return ProxyConfigJson.DeserializeCluster(ConfigJson);
    }
}

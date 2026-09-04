using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.Gateway.Proxying;

/// <summary>YARP 代理集群（目的地列表存 JSON 文本列，destinationId → 下游地址）。</summary>
public class ProxyCluster : FullAuditedAggregateRoot<Guid>
{
    /// <summary>YARP 集群 ID，全局唯一。</summary>
    public string ClusterId { get; private set; } = default!;

    /// <summary>目的地字典 JSON（destinationId → 下游地址）。</summary>
    public string DestinationsJson { get; private set; } = default!;

    protected ProxyCluster()
    {
    }

    public ProxyCluster(Guid id, string clusterId, string destinationsJson) : base(id)
    {
        ClusterId = Check.NotNullOrWhiteSpace(clusterId, nameof(clusterId), maxLength: 128);
        DestinationsJson = Check.NotNullOrWhiteSpace(destinationsJson, nameof(destinationsJson));
    }

    public void Update(string destinationsJson)
    {
        DestinationsJson = Check.NotNullOrWhiteSpace(destinationsJson, nameof(destinationsJson));
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

/// <summary>RouteConfig/ClusterConfig 与 ConfigJson 文本列之间的唯一序列化点（camelCase，全字段往返）。</summary>
public static class ProxyConfigJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeRoute(RouteConfig config)
    {
        return JsonSerializer.Serialize(config, Options);
    }

    public static RouteConfig DeserializeRoute(string json)
    {
        return JsonSerializer.Deserialize<RouteConfig>(json, Options)!;
    }

    public static string SerializeCluster(ClusterConfig config)
    {
        return JsonSerializer.Serialize(config, Options);
    }

    public static ClusterConfig DeserializeCluster(string json)
    {
        return JsonSerializer.Deserialize<ClusterConfig>(json, Options)!;
    }
}

using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DredgeAI.Gateway.Proxying;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway;

public class GatewayApplicationAutoMapperProfile : Profile
{
    public GatewayApplicationAutoMapperProfile()
    {
        // 输入：扁平 DTO → YARP
        CreateMap<ClusterDestinationDto, DestinationConfig>();
        CreateMap<ProxyClusterCreateUpdateDto, ClusterConfig>();

        CreateMap<ProxyRouteMatchDto, RouteMatch>();
        CreateMap<ProxyRouteCreateUpdateDto, RouteConfig>();

        // 输出：实体 → 扁平 DTO（RouteId/ClusterId/Order/Description/IsEnabled 及 Id/审计字段按约定映射）
        CreateMap<ProxyRoute, ProxyRouteDto>()
            .ForMember(d => d.AuthorizationPolicy, o => o.MapFrom(s => s.ToRouteConfig().AuthorizationPolicy))
            .ForMember(d => d.Match, o => o.MapFrom(s => ToMatchDto(s.ToRouteConfig().Match)));

        CreateMap<ProxyCluster, ProxyClusterDto>()
            .ForMember(d => d.Destinations, o => o.MapFrom(s => ToDestinationsDto(s.ToClusterConfig())));
    }

    private static ProxyRouteMatchDto ToMatchDto(RouteMatch? match) => new()
    {
        Path = match?.Path,
        Hosts = match?.Hosts
    };

    private static Dictionary<string, ClusterDestinationDto> ToDestinationsDto(ClusterConfig config) =>
        (config.Destinations ?? new Dictionary<string, DestinationConfig>())
            .ToDictionary(
                kv => kv.Key,
                kv => new ClusterDestinationDto { Address = kv.Value.Address, Health = kv.Value.Health });
}

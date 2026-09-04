using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;
using DredgeAI.Gateway.Proxying;

namespace DredgeAI.Gateway;

public class GatewayApplicationAutoMapperProfile : Profile
{
    public GatewayApplicationAutoMapperProfile()
    {
        CreateMap<ProxyRoute, ProxyRouteDto>()
            .ForMember(d => d.MatchHosts, o => o.MapFrom(s => DeserializeStringList(s.MatchHostsJson)))
            .ForMember(d => d.MatchMethods, o => o.MapFrom(s => DeserializeStringList(s.MatchMethodsJson)));

        CreateMap<ProxyCluster, ProxyClusterDto>()
            .ForMember(d => d.Destinations, o => o.MapFrom(s => DeserializeDestinations(s.DestinationsJson)));
    }

    private static List<string>? DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        return JsonSerializer.Deserialize<List<string>>(json);
    }

    private static Dictionary<string, string> DeserializeDestinations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }
}

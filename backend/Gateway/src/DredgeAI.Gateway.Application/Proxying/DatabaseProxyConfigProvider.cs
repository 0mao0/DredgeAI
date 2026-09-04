using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

/// <summary>
/// 从 DB 加载 YARP 路由/集群快照的 IProxyConfigProvider。
/// 写操作落库后由 AppService 调 Reload() 触发热重载；DB 不可用时返回空快照不抛异常。
/// </summary>
public sealed class DatabaseProxyConfigProvider : IProxyConfigProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseProxyConfigProvider> _logger;
    private volatile DatabaseProxyConfig _current;

    public DatabaseProxyConfigProvider(IServiceScopeFactory scopeFactory, ILogger<DatabaseProxyConfigProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _current = Load();
    }

    public IProxyConfig GetConfig() => _current;

    public void Reload()
    {
        var old = _current;
        _current = Load();
        old.SignalReload();
    }

    private DatabaseProxyConfig Load()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var routeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyRoute, Guid>>();
            var clusterRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyCluster, Guid>>();

            var routeEntities = AsyncHelper.RunSync(async () => await routeRepository.GetListAsync());
            var clusterEntities = AsyncHelper.RunSync(async () => await clusterRepository.GetListAsync());

            var routes = routeEntities
                .Where(x => x.IsEnabled)
                .Select(x => new RouteConfig
                {
                    RouteId = x.RouteId,
                    ClusterId = x.ClusterId,
                    Order = x.Order,
                    AuthorizationPolicy = x.AuthorizationPolicy,
                    Match = new RouteMatch
                    {
                        Path = x.MatchPath,
                        Hosts = DeserializeStringList(x.MatchHostsJson),
                        Methods = DeserializeStringList(x.MatchMethodsJson)
                    }
                })
                .ToList();

            var clusters = clusterEntities
                .Select(x => new ClusterConfig
                {
                    ClusterId = x.ClusterId,
                    Destinations = (DeserializeDestinations(x.DestinationsJson))
                        .ToDictionary(k => k.Key, v => new DestinationConfig { Address = v.Value })
                })
                .ToList();

            return new DatabaseProxyConfig(routes, clusters);
        }
        catch (Exception ex)
        {
            // DB 为空/表不存在（迁移未执行）时不崩溃，返回空快照
            _logger.LogError(ex, "加载代理配置失败，使用空快照");
            return new DatabaseProxyConfig([], []);
        }
    }

    private static IReadOnlyList<string>? DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        return JsonSerializer.Deserialize<List<string>>(json);
    }

    private static Dictionary<string, string> DeserializeDestinations(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }

    /// <summary>照 YARP InMemoryConfigProvider 的快照/变更令牌实现。</summary>
    private sealed class DatabaseProxyConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public DatabaseProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        public void SignalReload()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}

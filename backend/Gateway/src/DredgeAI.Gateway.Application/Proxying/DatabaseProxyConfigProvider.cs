using System;
using System.Collections.Generic;
using System.Linq;
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
/// 写操作落库后由 AppService 调 ReloadAsync() 触发热重载；DB 不可用时返回空快照不抛异常。
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
        _current = AsyncHelper.RunSync(LoadAsync);
    }

    public IProxyConfig GetConfig() => _current;

    public async Task ReloadAsync()
    {
        var old = _current;
        _current = await LoadAsync();
        old.SignalReload();
    }

    private async Task<DatabaseProxyConfig> LoadAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var routeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyRoute, Guid>>();
            var clusterRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyCluster, Guid>>();

            var routeEntities = await routeRepository.GetListAsync();
            var clusterEntities = await clusterRepository.GetListAsync();

            var enabledClusterIds = clusterEntities
                .Where(x => x.IsEnabled)
                .Select(x => x.ClusterId)
                .ToHashSet();

            var routes = routeEntities
                .Where(x => x.IsEnabled && enabledClusterIds.Contains(x.ClusterId))
                .Select(x => x.ToRouteConfig())
                .ToList();

            var clusters = clusterEntities
                .Where(x => x.IsEnabled)
                .Select(x => x.ToClusterConfig())
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

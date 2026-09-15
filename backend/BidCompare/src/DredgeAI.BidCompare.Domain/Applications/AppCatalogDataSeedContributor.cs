using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI.BidCompare.Applications;

/// <summary>
/// 首次启动种子：应用目录表为空时，从内嵌 seed-app-catalog.json 灌库（含全局排序行）。
/// 幂等；此后 DB 为唯一数据源。
/// </summary>
public class AppCatalogDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<AppCatalog, Guid> _catalogRepository;
    private readonly IRepository<AppOrder, Guid> _orderRepository;
    private readonly IVirtualFileProvider _virtualFileProvider;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<AppCatalogDataSeedContributor> _logger;

    public AppCatalogDataSeedContributor(
        IRepository<AppCatalog, Guid> catalogRepository,
        IRepository<AppOrder, Guid> orderRepository,
        IVirtualFileProvider virtualFileProvider,
        IGuidGenerator guidGenerator,
        ILogger<AppCatalogDataSeedContributor> logger)
    {
        _catalogRepository = catalogRepository;
        _orderRepository = orderRepository;
        _virtualFileProvider = virtualFileProvider;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (await _catalogRepository.GetCountAsync() > 0)
        {
            return;
        }

        var file = _virtualFileProvider.GetFileInfo("/Applications/seed-app-catalog.json");
        if (!file.Exists)
        {
            _logger.LogWarning("应用目录种子文件 /Applications/seed-app-catalog.json 不存在，跳过灌库");
            return;
        }

        List<SeedApp>? seeds;
        try
        {
            await using var stream = file.CreateReadStream();
            seeds = await JsonSerializer.DeserializeAsync<List<SeedApp>>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "应用目录种子 JSON 反序列化失败，跳过灌库");
            return;
        }

        if (seeds is null || seeds.Count == 0)
        {
            _logger.LogWarning("应用目录种子为空，跳过灌库");
            return;
        }

        var apps = new List<AppCatalog>();
        var orders = new List<AppOrder>();
        for (var i = 0; i < seeds.Count; i++)
        {
            var seed = seeds[i];
            if (seed.Id is null || seed.Name is null || seed.Icon is null || seed.Version is null)
            {
                _logger.LogWarning("应用目录种子第 {Index} 项缺少 id/name/icon/version，跳过", i);
                continue;
            }
            apps.Add(AppCatalog.CreateMain(
                seed.Id.Value, seed.Name, ParseCategory(seed.Category), seed.Icon, seed.Version,
                seed.Status == "已下架" ? AppCatalogStatus.Offline : AppCatalogStatus.Online,
                seed.Route, ParseScope(seed.Scope), seed.Manager, seed.UserCount, seed.ApiCalls, seed.UserRoute));
            orders.Add(new AppOrder(_guidGenerator.Create(), AppOrderLevel.Global, Guid.Empty, seed.Id.Value.ToString(), i + 1));

            if (seed.SubApps is null)
            {
                continue;
            }
            for (var j = 0; j < seed.SubApps.Count; j++)
            {
                var sub = seed.SubApps[j];
                if (sub.Id is null || sub.Name is null || sub.Icon is null || sub.Version is null)
                {
                    _logger.LogWarning("应用 {AppId} 第 {Index} 个子应用缺少 id/name/icon/version，跳过", seed.Id, j);
                    continue;
                }
                apps.Add(AppCatalog.CreateSub(
                    sub.Id.Value, seed.Id.Value, sub.Name, ParseCategory(sub.Category), sub.Icon, sub.Version,
                    sub.Status == "已下架" ? AppCatalogStatus.Unpublished : AppCatalogStatus.Published,
                    sub.Route, ParseScope(sub.Scope), sub.Description));
                orders.Add(new AppOrder(_guidGenerator.Create(), AppOrderLevel.Global, Guid.Empty, sub.Id.Value.ToString(), j + 1));
            }
        }

        await _catalogRepository.InsertManyAsync(apps);
        await _orderRepository.InsertManyAsync(orders);
    }

    private static AppCatalogCategory ParseCategory(string? value) => value switch
    {
        "经营" => AppCatalogCategory.Operation,
        "设计" => AppCatalogCategory.Design,
        "施工" => AppCatalogCategory.Construction,
        _ => AppCatalogCategory.General // 含 "通用" 与缺失
    };

    private static AppCatalogScope ParseScope(string? value) =>
        value == "私有" ? AppCatalogScope.Private : AppCatalogScope.Public;

    private class SeedApp
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Manager { get; set; }
        public string? Version { get; set; }
        public string? Status { get; set; }
        public int? UserCount { get; set; }
        public int? ApiCalls { get; set; }
        public string? Icon { get; set; }
        public string? Route { get; set; }
        public string? UserRoute { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
        public List<SeedSubApp>? SubApps { get; set; }
    }

    private class SeedSubApp
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public Guid? ParentAppId { get; set; }
        public string? Route { get; set; }
        public string? Icon { get; set; }
        public string? Version { get; set; }
        public string? Status { get; set; }
        public string? Scope { get; set; }
        public string? Description { get; set; }
    }
}

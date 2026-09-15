using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using DredgeAI.Permissions;
using DredgeAI.BidCompare.Permissions;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Http.Client;

namespace DredgeAI.BidCompare.Applications;

[RemoteService(false)]
public class ApplicationCatalogAppService : BidCompareAppService, IApplicationCatalogAppService
{
    private readonly IRepository<AppCatalog, Guid> _catalogRepository;
    private readonly IRepository<AppOrder, Guid> _orderRepository;
    private readonly IInternalPermissionQueryAppService _internalPermissionQueryAppService;

    public ApplicationCatalogAppService(
        IRepository<AppCatalog, Guid> catalogRepository,
        IRepository<AppOrder, Guid> orderRepository,
        IInternalPermissionQueryAppService internalPermissionQueryAppService)
    {
        _catalogRepository = catalogRepository;
        _orderRepository = orderRepository;
        _internalPermissionQueryAppService = internalPermissionQueryAppService;
    }

    public async Task<List<AppCatalogDto>> GetListAsync()
    {
        var (mains, subsByParent) = await LoadOrderedAsync();
        var result = mains.Select(x => ObjectMapper.Map<AppCatalog, AppCatalogDto>(x)).ToList();
        foreach (var dto in result)
        {
            if (subsByParent.TryGetValue(dto.Id, out var subs))
            {
                dto.SubApps = subs.Select(x => ObjectMapper.Map<AppCatalog, AppCatalogSubAppDto>(x)).ToList();
            }
        }
        var grantedRolesByKey = await LoadGrantedRolesByKeyAsync();
        foreach (var dto in result)
        {
            dto.GrantedRoles = grantedRolesByKey.GetValueOrDefault(dto.Id.ToString()) ?? [];
            foreach (var sub in dto.SubApps ?? [])
            {
                sub.GrantedRoles = grantedRolesByKey.GetValueOrDefault(sub.Id.ToString()) ?? [];
            }
        }
        return result;
    }

    public Task<List<CategoryConfigDto>> GetCategoriesAsync()
        => Task.FromResult(new List<CategoryConfigDto>
        {
            new() { Name = "general", Color = "blue" },
            new() { Name = "operation", Color = "green" },
            new() { Name = "design", Color = "purple" },
            new() { Name = "construction", Color = "gold" },
        });

    /// <summary>应用权限树：类型 → 主应用 → 子应用；类型名本地化（Enum:AppCatalogCategory.*），应用顺序沿用 AppOrder 全局排序行；含全部状态应用（授权不受发布状态影响）。</summary>
    public async Task<List<AppPermissionTreeNodeDto>> GetPermissionTreeAsync()
    {
        var (mains, subsByParent) = await LoadOrderedAsync();
        return mains
            .GroupBy(x => x.Category)
            .OrderBy(g => g.Key)
            .Select(g => new AppPermissionTreeNodeDto
            {
                Key = JsonNamingPolicy.SnakeCaseLower.ConvertName(g.Key.ToString()),
                Title = L[$"Enum:AppCatalogCategory.{g.Key}"].Value,
                Children = g.Select(app => new AppPermissionTreeNodeDto
                {
                    Key = app.Id.ToString(),
                    Title = app.Name,
                    Children = subsByParent.TryGetValue(app.Id, out var subs)
                        ? subs.Select(sub => new AppPermissionTreeNodeDto { Key = sub.Id.ToString(), Title = sub.Name }).ToList()
                        : null,
                }).ToList(),
            })
            .ToList();
    }

    /// <summary>当前用户拥有 View 权限且已发布的应用：无子应用主应用要求 Online+授权；含子应用主应用按子应用过滤（Published+授权），有任一满足即带出。授权条目经 Base 集成服务实时查询（无缓存陈旧），Provider 只看角色(R)/用户(U)。</summary>
    public async Task<List<AppCatalogDto>> GetAuthorizedListAsync()
    {
        var (mains, subsByParent) = await LoadOrderedAsync();
        List<ResourcePermissionGrantItemDto> grants;
        try
        {
            grants = await _internalPermissionQueryAppService.GetResourceGrantsAsync(
                BidComparePermissions.AppCatalog.Resources.Name,
                BidComparePermissions.AppCatalog.Resources.View);
        }
        catch (AbpRemoteCallException ex)
        {
            Logger.LogWarning(ex, "Base 集成服务调用失败");
            throw new BusinessException("BidCompare:BaseServiceFailed");
        }
        var userId = CurrentUser.Id?.ToString();
        var roles = CurrentUser.Roles;
        var grantedKeys = grants
            .Where(g => (g.ProviderName == "R" && roles.Contains(g.ProviderKey))
                        || (g.ProviderName == "U" && userId != null && g.ProviderKey == userId))
            .Select(g => g.ResourceKey)
            .ToHashSet();

        var result = new List<AppCatalogDto>();
        foreach (var app in mains)
        {
            if (subsByParent.TryGetValue(app.Id, out var subs) && subs.Count > 0)
            {
                var grantedSubs = subs
                    .Where(s => s.Status == AppCatalogStatus.Published && grantedKeys.Contains(s.Id.ToString()))
                    .ToList();
                if (grantedSubs.Count == 0)
                {
                    continue;
                }
                var dto = ObjectMapper.Map<AppCatalog, AppCatalogDto>(app);
                dto.SubApps = grantedSubs.Select(x => ObjectMapper.Map<AppCatalog, AppCatalogSubAppDto>(x)).ToList();
                result.Add(dto);
            }
            else
            {
                if (app.Status != AppCatalogStatus.Online || !grantedKeys.Contains(app.Id.ToString()))
                {
                    continue;
                }
                result.Add(ObjectMapper.Map<AppCatalog, AppCatalogDto>(app));
            }
        }
        return result;
    }

    public async Task<List<UserAppCardDto>> GetUserListAsync()
    {
        var (mains, subsByParent) = await LoadOrderedAsync();
        var cards = new List<UserAppCardDto>();
        foreach (var app in mains)
        {
            if (subsByParent.TryGetValue(app.Id, out var subs) && subs.Count > 0)
            {
                foreach (var sub in subs)
                {
                    if (sub.Status != AppCatalogStatus.Published)
                    {
                        continue;
                    }
                    cards.Add(new UserAppCardDto
                    {
                        Id = sub.Id,
                        ParentAppId = app.Id,
                        Title = sub.Name,
                        Description = string.IsNullOrWhiteSpace(sub.Description) ? $"{app.Name}的子应用" : sub.Description,
                        Category = sub.Category,
                        Icon = sub.Icon,
                        Status = "已授权",
                        Route = sub.Route ?? string.Empty,
                        Version = sub.Version,
                        Pinned = false,
                    });
                }
            }
            else
            {
                cards.Add(new UserAppCardDto
                {
                    Id = app.Id,
                    Title = app.Name,
                    Description = $"{app.Name}应用模块",
                    Category = app.Category,
                    Icon = app.Icon,
                    Status = app.Status == AppCatalogStatus.Offline ? "已下架" : "已授权",
                    Route = app.UserRoute ?? string.Empty,
                    Version = app.Version,
                    Pinned = false,
                });
            }
        }
        return cards;
    }

    public async Task SetAppStatusAsync(SetAppStatusInput input)
    {
        if (input.AppId == Guid.Empty)
        {
            throw new BusinessException("AppCatalog:InvalidAppId", "缺少应用 id");
        }
        var app = await FindMainAsync(input.AppId);
        app.SetStatus(input.Status);
        await _catalogRepository.UpdateAsync(app, autoSave: true);
    }

    public async Task SetSubStatusAsync(SetSubStatusInput input)
    {
        if (input.SubId == Guid.Empty)
        {
            throw new BusinessException("AppCatalog:InvalidSubId", "缺少子应用 id");
        }
        var sub = await FindSubAsync(input.SubId);
        sub.SetStatus(input.Status);
        await _catalogRepository.UpdateAsync(sub, autoSave: true);
    }

    public async Task SetAppCategoryAsync(SetAppFieldInput input)
    {
        var app = await FindMainAsync(input.AppId);
        app.SetCategory(input.Category);
        await _catalogRepository.UpdateAsync(app, autoSave: true);
    }

    public async Task SetSubCategoryAsync(SetSubFieldInput input)
    {
        var sub = await FindSubAsync(input.SubId);
        sub.SetCategory(input.Category);
        await _catalogRepository.UpdateAsync(sub, autoSave: true);
    }

    public async Task SetAppIconAsync(SetAppIconInput input)
    {
        var app = await FindMainAsync(input.AppId);
        app.SetIcon(input.Icon);
        await _catalogRepository.UpdateAsync(app, autoSave: true);
    }

    public async Task SetSubIconAsync(SetSubIconInput input)
    {
        var sub = await FindSubAsync(input.SubId);
        sub.SetIcon(input.Icon);
        await _catalogRepository.UpdateAsync(sub, autoSave: true);
    }

    public async Task<List<AppCatalogDto>> MoveAppAsync(MoveAppOrderInput input)
    {
        if (input.Direction is not ("up" or "down"))
        {
            throw new BusinessException("AppCatalog:InvalidDirection", "direction 只能是 up 或 down");
        }
        var (mains, _) = await LoadOrderedAsync();
        var index = mains.FindIndex(x => x.Id == input.AppId);
        if (index < 0)
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {input.AppId}");
        }
        var neighborIndex = input.Direction == "up" ? index - 1 : index + 1;
        if (neighborIndex < 0 || neighborIndex >= mains.Count)
        {
            return await GetListAsync(); // 已在边界，顺序不变
        }
        await SwapGlobalOrderAsync(mains[index].Id, mains[neighborIndex].Id);
        return await GetListAsync();
    }

    public async Task<List<AppCatalogDto>> MoveSubAppAsync(MoveSubAppOrderInput input)
    {
        if (input.Direction is not ("up" or "down"))
        {
            throw new BusinessException("AppCatalog:InvalidDirection", "direction 只能是 up 或 down");
        }
        var target = await _catalogRepository.FindAsync(input.SubId);
        if (target is null || target.ParentAppId is null)
        {
            throw new BusinessException("AppCatalog:SubAppNotFound", $"未找到子应用 {input.SubId}");
        }
        var (_, subsByParent) = await LoadOrderedAsync();
        var subs = subsByParent.TryGetValue(target.ParentAppId.Value, out var group) ? group : new List<AppCatalog>();
        var index = subs.FindIndex(x => x.Id == input.SubId);
        var neighborIndex = input.Direction == "up" ? index - 1 : index + 1;
        if (index < 0 || neighborIndex < 0 || neighborIndex >= subs.Count)
        {
            return await GetListAsync(); // 不在组内或已在边界，顺序不变
        }
        await SwapGlobalOrderAsync(subs[index].Id, subs[neighborIndex].Id);
        return await GetListAsync();
    }

    /// <summary>交换两个目录条目的全局排序行 SortOrder；任一侧缺排序行属数据异常，直接返回不抛错。</summary>
    private async Task SwapGlobalOrderAsync(Guid leftId, Guid rightId)
    {
        var orders = await _orderRepository.GetListAsync(x => x.Level == AppOrderLevel.Global);
        var left = orders.FirstOrDefault(x => x.TargetId == leftId.ToString());
        var right = orders.FirstOrDefault(x => x.TargetId == rightId.ToString());
        if (left is null || right is null)
        {
            return;
        }
        var temp = left.SortOrder;
        left.SetSortOrder(right.SortOrder);
        right.SetSortOrder(temp);
        await _orderRepository.UpdateAsync(left, autoSave: true);
        await _orderRepository.UpdateAsync(right, autoSave: true);
    }

    /// <summary>全量授权条目按 ResourceKey 聚合成角色名列表（仅 R；去重排序）。Base 集成服务不可达时降级为空（发布管理页核心操作不受角色列影响），记警告日志。</summary>
    private async Task<Dictionary<string, List<string>>> LoadGrantedRolesByKeyAsync()
    {
        List<ResourcePermissionGrantItemDto> grants;
        try
        {
            grants = await _internalPermissionQueryAppService.GetResourceGrantsAsync(
                BidComparePermissions.AppCatalog.Resources.Name,
                BidComparePermissions.AppCatalog.Resources.View);
        }
        catch (AbpRemoteCallException ex)
        {
            Logger.LogWarning(ex, "Base 集成服务调用失败，授权角色列降级为空");
            return [];
        }
        return grants
            .Where(g => g.ProviderName == "R")
            .GroupBy(g => g.ResourceKey)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProviderKey).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    /// <summary>加载目录并按全局排序行排序：主应用全局排序、子应用按母项分组组内排序；缺排序行排末尾。</summary>
    private async Task<(List<AppCatalog> Mains, Dictionary<Guid, List<AppCatalog>> SubsByParent)> LoadOrderedAsync()
    {
        var all = await _catalogRepository.GetListAsync();
        var orders = await _orderRepository.GetListAsync(x => x.Level == AppOrderLevel.Global);
        var orderMap = orders.ToDictionary(x => x.TargetId, x => x.SortOrder);

        var mains = all
            .Where(x => x.ParentAppId == null)
            .OrderBy(x => orderMap.GetValueOrDefault(x.Id.ToString(), int.MaxValue))
            .ThenBy(x => x.Id)
            .ToList();
        var subsByParent = all
            .Where(x => x.ParentAppId != null)
            .GroupBy(x => x.ParentAppId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(x => orderMap.GetValueOrDefault(x.Id.ToString(), int.MaxValue))
                    .ThenBy(x => x.Id)
                    .ToList());
        return (mains, subsByParent);
    }

    private async Task<AppCatalog> FindMainAsync(Guid appId)
    {
        var app = await _catalogRepository.FindAsync(appId);
        if (app is null || app.ParentAppId != null)
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {appId}");
        }
        return app;
    }

    private async Task<AppCatalog> FindSubAsync(Guid subId)
    {
        var sub = await _catalogRepository.FindAsync(subId);
        if (sub is null || sub.ParentAppId is null)
        {
            throw new BusinessException("AppCatalog:SubAppNotFound", $"未找到子应用 {subId}");
        }
        return sub;
    }
}

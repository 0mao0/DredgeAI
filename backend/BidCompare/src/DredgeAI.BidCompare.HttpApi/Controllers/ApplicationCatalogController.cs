using System.Collections.Generic;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 应用目录服务：admin 发布管理（发布/下架、分类、图标、全局排序）与 user-web 应用列表
/// 读写同一份后端目录（DB 持久化），保证两端联动。
/// </summary>
[Authorize]
[Route("api/bidcompare/app-catalog")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("应用目录")]
public class ApplicationCatalogController : BidCompareController
{
    private readonly IApplicationCatalogAppService _catalogAppService;

    public ApplicationCatalogController(IApplicationCatalogAppService catalogAppService)
    {
        _catalogAppService = catalogAppService;
    }

    /// <summary>GET /api/bidcompare/app-catalog 应用目录（含子应用，按全局排序行排序）</summary>
    /// <returns>应用目录列表</returns>
    [HttpGet]
    public Task<List<AppCatalogDto>> GetAsync()
        => _catalogAppService.GetListAsync();

    /// <summary>GET /api/bidcompare/app-catalog/categories 分类配置</summary>
    /// <returns>分类配置列表</returns>
    [HttpGet("categories")]
    public Task<List<CategoryConfigDto>> GetCategoriesAsync()
        => _catalogAppService.GetCategoriesAsync();

    /// <summary>GET /api/bidcompare/app-catalog/permission-tree 应用权限树（类型→主应用→子应用，类型名已本地化）</summary>
    /// <returns>三层权限树</returns>
    [HttpGet("permission-tree")]
    public Task<List<AppPermissionTreeNodeDto>> GetPermissionTreeAsync()
        => _catalogAppService.GetPermissionTreeAsync();

    /// <summary>GET /api/bidcompare/app-catalog/authorized 当前用户已授权且已发布的应用目录（侧边栏动态应用菜单用）</summary>
    /// <returns>授权应用目录列表（主应用 SubApps 已按授权过滤）</returns>
    [HttpGet("authorized")]
    public Task<List<AppCatalogDto>> GetAuthorizedListAsync()
        => _catalogAppService.GetAuthorizedListAsync();

    /// <summary>GET /api/bidcompare/app-catalog/list user-web 应用列表（按发布状态实时推导）</summary>
    /// <returns>已发布应用卡片列表</returns>
    [HttpGet("list")]
    public Task<List<UserAppCardDto>> GetUserListAsync()
        => _catalogAppService.GetUserListAsync();

    /// <summary>POST /api/bidcompare/app-catalog/status 发布/下架主应用</summary>
    /// <param name="input">应用 ID + 目标状态</param>
    [HttpPost("status")]
    public Task SetStatusAsync([FromBody] SetAppStatusInput input)
        => _catalogAppService.SetAppStatusAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/sub/status 发布/下架子应用</summary>
    /// <param name="input">子应用 ID + 目标状态</param>
    [HttpPost("sub/status")]
    public Task SetSubStatusAsync([FromBody] SetSubStatusInput input)
        => _catalogAppService.SetSubStatusAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/category 设置主应用分类</summary>
    /// <param name="input">应用 ID + 分类</param>
    [HttpPost("category")]
    public Task SetCategoryAsync([FromBody] SetAppFieldInput input)
        => _catalogAppService.SetAppCategoryAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/sub/category 设置子应用分类</summary>
    /// <param name="input">子应用 ID + 分类</param>
    [HttpPost("sub/category")]
    public Task SetSubCategoryAsync([FromBody] SetSubFieldInput input)
        => _catalogAppService.SetSubCategoryAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/icon 设置主应用图标</summary>
    /// <param name="input">应用 ID + 图标</param>
    [HttpPost("icon")]
    public Task SetIconAsync([FromBody] SetAppIconInput input)
        => _catalogAppService.SetAppIconAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/sub/icon 设置子应用图标</summary>
    /// <param name="input">子应用 ID + 图标</param>
    [HttpPost("sub/icon")]
    public Task SetSubIconAsync([FromBody] SetSubIconInput input)
        => _catalogAppService.SetSubIconAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/move 上移/下移主应用（交换全局排序行的 SortOrder），返回重排后的目录</summary>
    /// <param name="input">应用 ID + 移动方向（up|down）</param>
    /// <returns>重排后的应用目录</returns>
    [HttpPost("move")]
    public Task<List<AppCatalogDto>> MoveAsync([FromBody] MoveAppOrderInput input)
        => _catalogAppService.MoveAppAsync(input);

    /// <summary>POST /api/bidcompare/app-catalog/sub/move 上移/下移子应用（母项组内），返回重排后的目录</summary>
    /// <param name="input">子应用 ID + 移动方向（up|down）</param>
    /// <returns>重排后的应用目录</returns>
    [HttpPost("sub/move")]
    public Task<List<AppCatalogDto>> MoveSubAsync([FromBody] MoveSubAppOrderInput input)
        => _catalogAppService.MoveSubAppAsync(input);
}

using System.Collections.Generic;
using DredgeAI.BidCompare.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 应用目录服务：admin 发布管理（发布/下架、分类、图标）与 user-web 应用列表
/// 读写同一份后端目录（JSON 文件持久化），保证两端联动。
/// </summary>
[Authorize]
[Route("api/bidcompare/app-catalog")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("应用目录")]
public class ApplicationCatalogController : BidCompareController
{
    private readonly ApplicationCatalogStore _store;

    public ApplicationCatalogController(ApplicationCatalogStore store)
    {
        _store = store;
    }

    /// <summary>GET /api/bidcompare/app-catalog 应用目录（含子应用）</summary>
    /// <returns>应用目录列表</returns>
    [HttpGet]
    public List<CatalogApp> GetAsync()
        => _store.GetAll();

    /// <summary>GET /api/bidcompare/app-catalog/categories 分类配置</summary>
    /// <returns>分类配置列表</returns>
    [HttpGet("categories")]
    public List<CategoryConfigDto> GetCategoriesAsync()
        => _store.GetCategories();

    /// <summary>GET /api/bidcompare/app-catalog/list user-web 应用列表（按发布状态实时推导）</summary>
    /// <returns>已发布应用卡片列表</returns>
    [HttpGet("list")]
    public List<UserAppCardDto> GetUserListAsync()
        => _store.GetUserApps();

    /// <summary>POST /api/bidcompare/app-catalog/status 发布/下架主应用</summary>
    /// <param name="input">应用 ID + 目标状态</param>
    [HttpPost("status")]
    public void SetStatusAsync([FromBody] SetAppStatusInput input)
    {
        if (string.IsNullOrWhiteSpace(input.AppId))
        {
            throw new BusinessException("AppCatalog:InvalidAppId", "缺少应用 id");
        }

        if (!_store.SetAppStatus(input.AppId, input.Status))
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {input.AppId}");
        }
    }

    /// <summary>POST /api/bidcompare/app-catalog/sub/status 发布/下架子应用</summary>
    /// <param name="input">子应用 ID + 目标状态</param>
    [HttpPost("sub/status")]
    public void SetSubStatusAsync([FromBody] SetSubStatusInput input)
    {
        if (string.IsNullOrWhiteSpace(input.SubId))
        {
            throw new BusinessException("AppCatalog:InvalidSubId", "缺少子应用 id");
        }

        if (!_store.SetSubStatus(input.SubId, input.Status))
        {
            throw new BusinessException("AppCatalog:SubAppNotFound", $"未找到子应用 {input.SubId}");
        }
    }

    /// <summary>POST /api/bidcompare/app-catalog/category 设置主应用分类</summary>
    /// <param name="input">应用 ID + 分类</param>
    [HttpPost("category")]
    public void SetCategoryAsync([FromBody] SetAppFieldInput input)
    {
        if (!_store.SetCategory(input.AppId, input.Category))
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {input.AppId}");
        }
    }

    /// <summary>POST /api/bidcompare/app-catalog/sub/category 设置子应用分类</summary>
    /// <param name="input">子应用 ID + 分类</param>
    [HttpPost("sub/category")]
    public void SetSubCategoryAsync([FromBody] SetSubFieldInput input)
    {
        if (!_store.SetCategory(input.SubId, input.Category))
        {
            throw new BusinessException("AppCatalog:SubAppNotFound", $"未找到子应用 {input.SubId}");
        }
    }

    /// <summary>POST /api/bidcompare/app-catalog/icon 设置主应用图标</summary>
    /// <param name="input">应用 ID + 图标</param>
    [HttpPost("icon")]
    public void SetIconAsync([FromBody] SetAppIconInput input)
    {
        if (!_store.SetIcon(input.AppId, input.Icon))
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {input.AppId}");
        }
    }

    /// <summary>POST /api/bidcompare/app-catalog/sub/icon 设置子应用图标</summary>
    /// <param name="input">子应用 ID + 图标</param>
    [HttpPost("sub/icon")]
    public void SetSubIconAsync([FromBody] SetSubIconInput input)
    {
        if (!_store.SetIcon(input.SubId, input.Icon))
        {
            throw new BusinessException("AppCatalog:SubAppNotFound", $"未找到子应用 {input.SubId}");
        }
    }
}

public class SetAppStatusInput
{
    public string AppId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public class SetSubStatusInput
{
    public string SubId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public class SetAppFieldInput
{
    public string AppId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

public class SetSubFieldInput
{
    public string SubId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}

public class SetAppIconInput
{
    public string AppId { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;
}

public class SetSubIconInput
{
    public string SubId { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;
}

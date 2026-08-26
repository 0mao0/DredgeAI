using System.Collections.Generic;
using DredgeAI.BidCompare.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 应用目录服务：admin 发布管理（发布/下架、分类、图标）与 user-web 应用列表
/// 读写同一份后端目录（JSON 文件持久化），保证两端联动。
/// </summary>
[Route("api/admin/applications")]
[Route("api/app")]
[Authorize]
public class ApplicationCatalogController : AbpControllerBase
{
    private readonly ApplicationCatalogStore _store;

    public ApplicationCatalogController(ApplicationCatalogStore store)
    {
        _store = store;
    }

    /// <summary>GET /api/admin/applications 应用目录（含子应用）。</summary>
    [HttpGet]
    public List<CatalogApp> Get()
        => _store.GetAll();

    /// <summary>GET /api/admin/applications/categories 分类配置。</summary>
    [HttpGet("categories")]
    public List<CategoryConfigDto> GetCategories()
        => _store.GetCategories();

    /// <summary>GET /api/app/list user-web 应用列表（按发布状态实时推导）。</summary>
    [HttpGet("list")]
    public List<UserAppCardDto> GetUserList()
        => _store.GetUserApps();

    /// <summary>POST /api/admin/applications/status 发布/下架主应用。</summary>
    [HttpPost("status")]
    public void SetStatus([FromBody] SetAppStatusInput input)
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

    /// <summary>POST /api/admin/applications/sub/status 发布/下架子应用。</summary>
    [HttpPost("sub/status")]
    public void SetSubStatus([FromBody] SetSubStatusInput input)
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

    /// <summary>POST /api/admin/applications/category 设置主应用分类。</summary>
    [HttpPost("category")]
    public void SetCategory([FromBody] SetAppFieldInput input)
    {
        if (!_store.SetCategory(input.AppId, input.Category))
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {input.AppId}");
        }
    }

    /// <summary>POST /api/admin/applications/sub/category 设置子应用分类。</summary>
    [HttpPost("sub/category")]
    public void SetSubCategory([FromBody] SetSubFieldInput input)
    {
        if (!_store.SetCategory(input.SubId, input.Category))
        {
            throw new BusinessException("AppCatalog:SubAppNotFound", $"未找到子应用 {input.SubId}");
        }
    }

    /// <summary>POST /api/admin/applications/icon 设置主应用图标。</summary>
    [HttpPost("icon")]
    public void SetIcon([FromBody] SetAppIconInput input)
    {
        if (!_store.SetIcon(input.AppId, input.Icon))
        {
            throw new BusinessException("AppCatalog:AppNotFound", $"未找到应用 {input.AppId}");
        }
    }

    /// <summary>POST /api/admin/applications/sub/icon 设置子应用图标。</summary>
    [HttpPost("sub/icon")]
    public void SetSubIcon([FromBody] SetSubIconInput input)
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

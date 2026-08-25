using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.MenuManagement;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>菜单管理接口</summary>
/// <remarks>菜单支持多级树形结构，通过 ParentId 建立父子关系。系统菜单（IsStatic=true）受保护不可修改和删除</remarks>
[Authorize]
[Route("api/base/menus")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("菜单管理")]
public class MenuInfoController : DredgeAIBaseController, IMenuInfoAppService
{
    private readonly IMenuInfoAppService _service;

    public MenuInfoController(IMenuInfoAppService service)
    {
        _service = service;
    }

    /// <summary>分页查询菜单列表</summary>
    /// <param name="input">查询条件，支持按名称关键词、类型和启用状态筛选</param>
    /// <returns>分页的菜单列表</returns>
    [HttpGet]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<PagedResultDto<MenuInfoDto>> GetListAsync([FromQuery] GetMenuInfoListInput input)
        => _service.GetListAsync(input);

    /// <summary>按 ID 获取单个菜单</summary>
    /// <param name="id">菜单 ID</param>
    /// <returns>菜单详情，包含子级列表</returns>
    [HttpGet("{id}")]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<MenuInfoDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>创建菜单</summary>
    /// <param name="input">菜单创建参数，Name 在同层级下必须唯一</param>
    /// <returns>创建成功的菜单</returns>
    [HttpPost]
    [Authorize(DredgeAIBasePermissions.Menus.Create)]
    public Task<MenuInfoDto> CreateAsync([FromBody] MenuInfoCreateUpdateDto input)
        => _service.CreateAsync(input);

    /// <summary>更新菜单</summary>
    /// <param name="id">菜单 ID</param>
    /// <param name="input">菜单更新参数，ParentId 不能指向自身或后代节点</param>
    /// <returns>更新后的菜单</returns>
    [HttpPut("{id}")]
    [Authorize(DredgeAIBasePermissions.Menus.Update)]
    public Task<MenuInfoDto> UpdateAsync(Guid id, [FromBody] MenuInfoCreateUpdateDto input)
        => _service.UpdateAsync(id, input);

    /// <summary>删除菜单</summary>
    /// <param name="id">菜单 ID</param>
    [HttpDelete("{id}")]
    [Authorize(DredgeAIBasePermissions.Menus.Delete)]
    public Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);

    /// <summary>获取菜单树形结构</summary>
    /// <param name="input">查询条件，支持按类型和启用状态筛选</param>
    /// <returns>菜单树节点列表，Children 包含递归子菜单</returns>
    [HttpGet("tree")]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<List<MenuInfoDto>> GetTreeAsync([FromQuery] GetMenuInfoTreeInput input)
        => _service.GetTreeAsync(input);

    /// <summary>获取当前用户拥有权限的菜单树</summary>
    /// <remarks>仅返回当前用户有权访问的 Directory 和 Menu 类型菜单。无权限码的公共菜单对所有已登录用户可见，有权限码的菜单需要用户拥有对应权限。</remarks>
    /// <param name="input">查询条件，支持按名称关键词、类型和启用状态筛选</param>
    /// <returns>经过权限过滤的菜单树</returns>
    [HttpGet("my-permitted-tree")]
    [Authorize]
    public Task<List<MenuTreeNodeDto>> GetCurrentUserPermittedTreeAsync([FromQuery] GetMenuInfoTreeInput input)
        => _service.GetCurrentUserPermittedTreeAsync(input);
}

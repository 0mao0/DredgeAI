using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace DredgeAI.Controllers;

/// <summary>
/// 角色管理控制器，替换 ABP 内置的 <see cref="IdentityRoleController"/>，
/// 使用 Base 模块自定义权限校验角色 CRUD 操作。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityRoleController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/identity/roles")]
[Tags("角色管理")]
public class MyIdentityRoleController : IdentityRoleController
{
    public MyIdentityRoleController(IIdentityRoleAppService roleAppService) : base(roleAppService)
    {
    }

    /// <summary>
    /// 获取所有角色的完整列表（不分页）。
    /// </summary>
    /// <returns>全部角色列表。</returns>
    [HttpGet]
    [Route("all")]
    public override Task<ListResultDto<IdentityRoleDto>> GetAllListAsync()
    {
        return RoleAppService.GetAllListAsync();
    }

    /// <summary>
    /// 获取分页的角色列表，支持按角色名模糊筛选。
    /// </summary>
    /// <param name="input">包含分页、排序和筛选条件的 <see cref="GetIdentityRolesInput"/> 对象。</param>
    /// <returns>分页的角色数据列表。</returns>
    [HttpGet]
    public override Task<PagedResultDto<IdentityRoleDto>> GetListAsync(GetIdentityRolesInput input)
    {
        return RoleAppService.GetListAsync(input);
    }

    /// <summary>
    /// 根据角色ID获取单个角色详情。
    /// </summary>
    /// <param name="id">角色的唯一标识符 (GUID)。</param>
    /// <returns>包含角色信息的 <see cref="IdentityRoleDto"/> 对象。</returns>
    [HttpGet]
    [Route("{id}")]
    public override Task<IdentityRoleDto> GetAsync(Guid id)
    {
        return RoleAppService.GetAsync(id);
    }

    /// <summary>
    /// 创建新角色，需指定角色名称、是否为默认角色和是否公开。
    /// </summary>
    /// <param name="input">包含角色创建信息的 <see cref="IdentityRoleCreateDto"/> 对象。</param>
    /// <returns>创建成功的角色信息。</returns>
    [HttpPost]
    public override Task<IdentityRoleDto> CreateAsync(IdentityRoleCreateDto input)
    {
        return RoleAppService.CreateAsync(input);
    }

    /// <summary>
    /// 更新指定角色的名称、默认标记和公开标记。
    /// </summary>
    /// <param name="id">要更新的角色ID。</param>
    /// <param name="input">包含更新内容的 <see cref="IdentityRoleUpdateDto"/> 对象。</param>
    /// <returns>更新后的角色信息。</returns>
    [HttpPut]
    [Route("{id}")]
    public override Task<IdentityRoleDto> UpdateAsync(Guid id, IdentityRoleUpdateDto input)
    {
        return RoleAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// 删除指定角色。
    /// </summary>
    /// <param name="id">要删除的角色ID。</param>
    [HttpDelete]
    [Route("{id}")]
    public override Task DeleteAsync(Guid id)
    {
        return RoleAppService.DeleteAsync(id);
    }
}

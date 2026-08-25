using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace DredgeAI.Controllers;

/// <summary>
/// 用户管理控制器，替换 ABP 内置的 <see cref="IdentityUserController"/>，
/// 使用 Base 模块自定义权限校验用户 CRUD 操作。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityUserController), IncludeSelf = true)]
[Tags("用户管理")]
[RemoteService(false)]
public class MyIdentityUserController : IdentityUserController
{
    public MyIdentityUserController(IIdentityUserAppService userAppService) : base(userAppService)
    {
    }

    /// <summary>
    /// 根据用户ID获取单个用户详情。
    /// </summary>
    /// <param name="id">用户的唯一标识符 (GUID)。</param>
    /// <returns>包含用户信息的 <see cref="IdentityUserDto"/> 对象。</returns>
    [HttpGet]
    [Route("{id}")]
    public override Task<IdentityUserDto> GetAsync(Guid id)
    {
        return UserAppService.GetAsync(id);
    }

    /// <summary>
    /// 获取分页的用户列表，支持按用户名或邮箱模糊筛选。
    /// </summary>
    /// <param name="input">包含分页、排序和筛选条件的 <see cref="GetIdentityUsersInput"/> 对象。</param>
    /// <returns>分页的用户数据列表。</returns>
    [HttpGet]
    public override Task<PagedResultDto<IdentityUserDto>> GetListAsync(GetIdentityUsersInput input)
    {
        return UserAppService.GetListAsync(input);
    }

    /// <summary>
    /// 创建新用户，包含用户名、邮箱、密码和角色分配。
    /// </summary>
    /// <param name="input">包含用户创建信息的 <see cref="IdentityUserCreateDto"/> 对象。</param>
    /// <returns>创建成功的用户信息。</returns>
    [HttpPost]
    public override Task<IdentityUserDto> CreateAsync(IdentityUserCreateDto input)
    {
        return UserAppService.CreateAsync(input);
    }

    /// <summary>
    /// 更新指定用户的基本信息、邮箱、手机号和角色。
    /// </summary>
    /// <param name="id">要更新的用户ID。</param>
    /// <param name="input">包含更新内容的 <see cref="IdentityUserUpdateDto"/> 对象。</param>
    /// <returns>更新后的用户信息。</returns>
    [HttpPut]
    [Route("{id}")]
    public override Task<IdentityUserDto> UpdateAsync(Guid id, IdentityUserUpdateDto input)
    {
        return UserAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// 删除指定用户，不允许删除当前登录用户自身。
    /// </summary>
    /// <param name="id">要删除的用户ID。</param>
    [HttpDelete]
    [Route("{id}")]
    public override Task DeleteAsync(Guid id)
    {
        return UserAppService.DeleteAsync(id);
    }

    /// <summary>
    /// 根据用户ID查找用户信息（轻量查询）。
    /// </summary>
    /// <param name="id">用户ID。</param>
    /// <returns>包含用户基本信息的 DTO。</returns>
    [HttpGet]
    [Route("by-id/{id}")]
    public override Task<IdentityUserDto> FindByIdAsync(Guid id)
    {
        return UserAppService.FindByIdAsync(id);
    }

    /// <summary>
    /// 获取指定用户已分配的角色列表。
    /// </summary>
    /// <param name="id">用户ID。</param>
    /// <returns>该用户拥有的角色列表。</returns>
    [HttpGet]
    [Route("{id}/roles")]
    public override Task<ListResultDto<IdentityRoleDto>> GetRolesAsync(Guid id)
    {
        return UserAppService.GetRolesAsync(id);
    }

    /// <summary>
    /// 更新指定用户的角色分配。仅操作者拥有的角色可被分配，
    /// 操作者不拥有的角色将保持不变。
    /// </summary>
    /// <param name="id">用户ID。</param>
    /// <param name="input">包含新角色名称列表的输入对象。</param>
    [HttpPut]
    [Route("{id}/roles")]
    public override Task UpdateRolesAsync(Guid id, IdentityUserUpdateRolesDto input)
    {
        return UserAppService.UpdateRolesAsync(id, input);
    }

    /// <summary>
    /// 根据用户名查找用户信息。
    /// </summary>
    /// <param name="userName">要查找的用户名。</param>
    /// <returns>匹配的用户信息，未找到则返回 null。</returns>
    [HttpGet]
    [Route("by-username/{userName}")]
    public override Task<IdentityUserDto> FindByUsernameAsync(string userName)
    {
        return UserAppService.FindByUsernameAsync(userName);
    }

    /// <summary>
    /// 根据邮箱地址查找用户信息。
    /// </summary>
    /// <param name="email">要查找的邮箱地址。</param>
    /// <returns>匹配的用户信息，未找到则返回 null。</returns>
    [HttpGet]
    [Route("by-email/{email}")]
    public override Task<IdentityUserDto> FindByEmailAsync(string email)
    {
        return UserAppService.FindByEmailAsync(email);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.Permissions;
using DredgeAI.UserManagement;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>用户管理接口</summary>
[Authorize]
[Route("api/base/users")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("用户管理")]
public class UserController : DredgeAIBaseController, IUserAppService
{
    private readonly IUserAppService _service;

    public UserController(IUserAppService service)
    {
        _service = service;
    }

    /// <summary>分页查询用户列表</summary>
    /// <param name="input">查询条件，支持按关键字、组织、启用状态筛选</param>
    /// <returns>分页的用户列表</returns>
    [HttpGet]
    [Authorize(DredgeAIBasePermissions.Users.Default)]
    public Task<PagedResultDto<UserDto>> GetListAsync([FromQuery] GetUserListInput input)
        => _service.GetListAsync(input);

    /// <summary>按 ID 获取用户</summary>
    /// <param name="id">用户 ID</param>
    /// <returns>用户详情</returns>
    [HttpGet("{id}")]
    [Authorize(DredgeAIBasePermissions.Users.Default)]
    public Task<UserDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>创建用户</summary>
    /// <param name="input">用户创建参数</param>
    /// <returns>创建成功的用户</returns>
    [HttpPost]
    [Authorize(DredgeAIBasePermissions.Users.Create)]
    public Task<UserDto> CreateAsync([FromBody] CreateUserDto input)
        => _service.CreateAsync(input);

    /// <summary>更新用户</summary>
    /// <param name="id">用户 ID</param>
    /// <param name="input">用户更新参数</param>
    /// <returns>更新后的用户</returns>
    [HttpPut("{id}")]
    [Authorize(DredgeAIBasePermissions.Users.Update)]
    public Task<UserDto> UpdateAsync(Guid id, [FromBody] UpdateUserDto input)
        => _service.UpdateAsync(id, input);

    /// <summary>删除用户</summary>
    /// <param name="id">用户 ID</param>
    [HttpDelete("{id}")]
    [Authorize(DredgeAIBasePermissions.Users.Delete)]
    public Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);

    /// <summary>切换用户启用状态</summary>
    /// <param name="id">用户 ID</param>
    /// <param name="isActive">是否启用</param>
    [HttpPut("{id}/change-active")]
    [Authorize(DredgeAIBasePermissions.Users.Update)]
    public Task ChangeActiveAsync(Guid id, [FromQuery] bool isActive)
        => _service.ChangeActiveAsync(id, isActive);

    /// <summary>重置密码</summary>
    /// <param name="id">用户 ID</param>
    /// <param name="input">新密码</param>
    [HttpPost("{id}/reset-password")]
    [Authorize(DredgeAIBasePermissions.Users.Update)]
    public Task ResetPasswordAsync(Guid id, [FromBody] ResetPasswordInput input)
        => _service.ResetPasswordAsync(id, input.Password);

    /// <summary>校验手机号是否已存在</summary>
    /// <param name="phoneNumber">手机号</param>
    /// <param name="excludeId">排除的用户 ID（编辑场景）</param>
    /// <returns>true 表示可用，false 表示已存在</returns>
    [HttpGet("check-phone")]
    [AllowAnonymous]
    public Task<bool> CheckPhoneAsync([FromQuery] string phoneNumber, [FromQuery] Guid? excludeId = null)
        => _service.CheckPhoneAsync(phoneNumber, excludeId);

    Task IUserAppService.ResetPasswordAsync(Guid id, string password)
        => _service.ResetPasswordAsync(id, password);

    public record ResetPasswordInput
    {
        public string Password { get; set; } = string.Empty;
    }
}

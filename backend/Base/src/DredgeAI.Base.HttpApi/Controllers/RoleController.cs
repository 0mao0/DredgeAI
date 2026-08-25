using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.Permissions;
using DredgeAI.UserManagement;
using Volo.Abp;

namespace DredgeAI.Controllers;

[Authorize]
[Route("api/base/roles")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("角色管理")]
public class RoleController : DredgeAIBaseController,IUserRoleAppService
{
    private readonly IUserRoleAppService _service;

    public RoleController(IUserRoleAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// 批量设置角色关联的用户（全量替换模式）
    /// </summary>
    /// <param name="input">
    /// 包含角色名称和用户 ID 列表的输入数据
    /// </param>
    /// <returns>
    /// 表示异步操作的任务对象
    /// </returns>
    [HttpPost("batch-set-user")]
    [Authorize(DredgeAIBasePermissions.Users.Update)]
    public Task BatchSetRoleUsersAsync([FromBody] BatchSetRoleUsersInput input)
        => _service.BatchSetRoleUsersAsync(input);

    /// <summary>
    /// 从指定角色中移除用户
    /// </summary>
    /// <param name="roleName">角色的名称。</param>
    /// <param name="userId">用户的唯一标识符。</param>
    /// <returns>表示异步操作的任务</returns>
    [HttpDelete("remove-role-user")]
    [Authorize(DredgeAIBasePermissions.Users.Update)]
    public Task RemoveRoleUserAsync([FromQuery] string roleName, [FromQuery] Guid userId)
        => _service.RemoveRoleUserAsync(roleName, userId);
}

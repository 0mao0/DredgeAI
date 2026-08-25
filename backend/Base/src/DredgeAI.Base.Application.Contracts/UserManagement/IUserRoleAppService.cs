using Volo.Abp.Application.Services;

namespace DredgeAI.UserManagement;

/// <summary>角色-用户管理应用服务接口</summary>
public interface IUserRoleAppService : IApplicationService
{
    /// <summary>批量设置用户到角色（全量替换模式）</summary>
    Task BatchSetRoleUsersAsync(BatchSetRoleUsersInput input);

    /// <summary>从角色中移除单个用户</summary>
    Task RemoveRoleUserAsync(string roleName, Guid userId);
}

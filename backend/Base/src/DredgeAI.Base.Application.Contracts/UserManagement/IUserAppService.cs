using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.UserManagement;

/// <summary>用户管理应用服务接口</summary>
public interface IUserAppService : IApplicationService
{
    /// <summary>按 ID 获取用户</summary>
    Task<UserDto> GetAsync(Guid id);

    /// <summary>分页查询用户列表</summary>
    Task<PagedResultDto<UserDto>> GetListAsync(GetUserListInput input);

    /// <summary>创建用户</summary>
    Task<UserDto> CreateAsync(CreateUserDto input);

    /// <summary>更新用户</summary>
    Task<UserDto> UpdateAsync(Guid id, UpdateUserDto input);

    /// <summary>删除用户</summary>
    Task DeleteAsync(Guid id);

    /// <summary>切换用户启用状态</summary>
    Task ChangeActiveAsync(Guid id, bool isActive);

    /// <summary>重置密码</summary>
    Task ResetPasswordAsync(Guid id, string password);

    /// <summary>校验手机号是否已存在</summary>
    Task<bool> CheckPhoneAsync(string phoneNumber, Guid? excludeId = null);
}

using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Identity;

namespace DredgeAI.UserManagement;

public class UserRoleAppService : DredgeAIBaseAppService, IUserRoleAppService
{
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityRoleRepository _identityRoleRepository;

    public UserRoleAppService(
        IdentityRoleManager identityRoleManager,
        IdentityUserManager identityUserManager,
        IIdentityRoleRepository identityRoleRepository)
    {
        _identityRoleManager = identityRoleManager;
        _identityUserManager = identityUserManager;
        _identityRoleRepository = identityRoleRepository;
    }

    public async Task BatchSetRoleUsersAsync(BatchSetRoleUsersInput input)
    {
        var role = await _identityRoleManager.FindByNameAsync(input.RoleName);
        if (role == null)
        {
            throw new BusinessException("DredgeAIBase:RoleNotFound")
                .WithData("RoleName", input.RoleName);
        }

        var currentUsers = await _identityUserManager.GetUsersInRoleAsync(role.Name);
        var currentUserIds = currentUsers.Select(u => u.Id).ToHashSet();
        var targetUserIds = input.UserIds.ToHashSet();

        var toAdd = targetUserIds.Except(currentUserIds).ToList();
        var toRemove = currentUserIds.Except(targetUserIds).ToList();

        foreach (var userId in toRemove)
        {
            var user = await _identityUserManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                await _identityUserManager.RemoveFromRoleAsync(user, role.Name);
            }
        }

        foreach (var userId in toAdd)
        {
            var user = await _identityUserManager.FindByIdAsync(userId.ToString());
            if (user != null)
            {
                await _identityUserManager.AddToRoleAsync(user, role.Name);
            }
        }
    }

    public async Task RemoveRoleUserAsync(string roleName, Guid userId)
    {
        var role = await _identityRoleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            throw new BusinessException("DredgeAIBase:RoleNotFound")
                .WithData("RoleName", roleName);
        }

        var user = await _identityUserManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return;
        }

        await _identityUserManager.RemoveFromRoleAsync(user, role.Name);
    }

    public async Task<List<RoleUserCountDto>> GetRoleUserCountsAsync()
    {
        var roles = await _identityRoleRepository.GetListAsync();
        var result = new List<RoleUserCountDto>();
        foreach (var role in roles)
        {
            var users = await _identityUserManager.GetUsersInRoleAsync(role.Name);
            result.Add(new RoleUserCountDto { RoleName = role.Name, UserCount = users.Count });
        }

        return result;
    }
}

using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace DredgeAI.UserManagement;

public class UserAppService : DredgeAIBaseAppService, IUserAppService
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityUserExtensionRepository _identityUserExtensionRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;

    public UserAppService(
        IUserRepository userRepository,
        IIdentityUserExtensionRepository identityUserExtensionRepository,
        IdentityUserManager identityUserManager,
        IRepository<IdentityRole, Guid> roleRepository)
    {
        _userRepository = userRepository;
        _identityUserExtensionRepository = identityUserExtensionRepository;
        _identityUserManager = identityUserManager;
        _roleRepository = roleRepository;
    }

    public async Task<PagedResultDto<UserDto>> GetListAsync(GetUserListInput input)
    {
        List<IdentityUser> users;
        long totalCount;

        if (input.RoleId.HasValue || !string.IsNullOrWhiteSpace(input.RoleName))
        {
            users = await _userRepository.GetPagedListByRoleAsync(
                input.RoleId, input.RoleName, input.Keyword, input.IsActive,
                input.SkipCount, input.MaxResultCount, input.Sorting, input.OrganizationUnitId);
            totalCount = await _userRepository.GetCountByRoleAsync(
                input.RoleId, input.RoleName, input.Keyword, input.IsActive, input.OrganizationUnitId);
        }
        else
        {
            users = await _userRepository.GetPagedListAsync(
                input.Keyword, input.IsActive, input.SkipCount, input.MaxResultCount, input.Sorting, input.OrganizationUnitId);
            totalCount = await _userRepository.GetCountAsync(input.Keyword, input.IsActive, input.OrganizationUnitId);
        }

        var userIds = users.Select(u => u.Id).ToList();
        if (userIds.Count == 0)
        {
            return new PagedResultDto<UserDto>(totalCount, []);
        }

        var extensions = await _identityUserExtensionRepository.GetListByUserIdsAsync(userIds);
        var orgUnits = await _userRepository.GetOrganizationUnitsByUserIdsAsync(userIds);
        var roles = await _userRepository.GetRolesByUserIdsAsync(userIds);

        var items = users.Select(user => MapToDto(user, extensions, orgUnits, roles)).ToList();

        return new PagedResultDto<UserDto>(totalCount, items);
    }

    public async Task<UserDto> GetAsync(Guid id)
    {
        var user = await _identityUserManager.GetByIdAsync(id);
        var extension = await _identityUserExtensionRepository.FindAsync(e => e.UserId == id);

        var orgUnits = await _userRepository.GetOrganizationUnitsByUserIdsAsync([id]);
        var roles = await _userRepository.GetRolesByUserIdsAsync([id]);

        return MapToDto(user, extension, orgUnits, roles);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            var existingPhone = await _userRepository.FindByPhoneNumberAsync(input.PhoneNumber);
            if (existingPhone != null)
            {
                throw new BusinessException("DredgeAIBase:PhoneNumberAlreadyExists")
                    .WithData("PhoneNumber", input.PhoneNumber);
            }
        }

        var user = new IdentityUser(
            GuidGenerator.Create(),
            input.UserName,
            $"{Guid.NewGuid():N}@null.local",
            CurrentTenant.Id)
        {
            Name = input.Name,
        };
        user.SetPhoneNumber(input.PhoneNumber, confirmed: false);

        var result = await _identityUserManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            throw new BusinessException("DredgeAIBase:UserCreationFailed")
                .WithData("Errors", string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (input.ExpireTime.HasValue)
        {
            var extension = new IdentityUserExtension(
                GuidGenerator.Create(), user.Id, input.ExpireTime);
            await _identityUserExtensionRepository.InsertAsync(extension);
        }

        if (input.RoleNames is { Count: > 0 })
        {
            await _identityUserManager.AddToRolesAsync(user, input.RoleNames);
        }

        if (input.OrganizationIds is { Count: > 0 })
        {
            foreach (var orgId in input.OrganizationIds)
            {
                await _identityUserManager.AddToOrganizationUnitAsync(user.Id, orgId);
            }
        }

        if (CurrentUnitOfWork != null)
            await CurrentUnitOfWork.SaveChangesAsync();

        var extensions = await _identityUserExtensionRepository.GetListByUserIdsAsync([user.Id]);
        var orgUnits = await _userRepository.GetOrganizationUnitsByUserIdsAsync([user.Id]);
        var roles = await _userRepository.GetRolesByUserIdsAsync([user.Id]);

        return MapToDto(user, extensions, orgUnits, roles);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto input)
    {
        var user = await _identityUserManager.GetByIdAsync(id);

        if (input.Name is not null)
        {
            user.Name = input.Name;
        }

        if (input.PhoneNumber is not null)
        {
            user.SetPhoneNumber(input.PhoneNumber, confirmed: false);
        }

        if (input.Name is not null || input.PhoneNumber is not null)
        {
            await _identityUserManager.UpdateAsync(user);
        }

        if (input.ExpireTime is not null)
        {
            var extension = await _identityUserExtensionRepository.FindAsync(e => e.UserId == id);
            if (extension != null)
            {
                extension.SetExpireTime(input.ExpireTime);
                await _identityUserExtensionRepository.UpdateAsync(extension);
            }
            else
            {
                extension = new IdentityUserExtension(GuidGenerator.Create(), id, input.ExpireTime);
                await _identityUserExtensionRepository.InsertAsync(extension);
            }
        }

        if (input.RoleNames is not null)
        {
            var currentRoles = await _identityUserManager.GetRolesAsync(user);
            var rolesToAdd = input.RoleNames.Except(currentRoles).ToList();
            var rolesToRemove = currentRoles.Except(input.RoleNames).ToList();

            if (rolesToRemove.Count > 0)
            {
                await _identityUserManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            if (rolesToAdd.Count > 0)
            {
                await _identityUserManager.AddToRolesAsync(user, rolesToAdd);
            }
        }

        if (input.OrganizationIds is not null)
        {
            await _identityUserManager.SetOrganizationUnitsAsync(user.Id, [.. input.OrganizationIds]);
        }

        if (CurrentUnitOfWork != null)
            await CurrentUnitOfWork.SaveChangesAsync();
        var extensions = await _identityUserExtensionRepository.GetListByUserIdsAsync([id]);
        var orgUnits = await _userRepository.GetOrganizationUnitsByUserIdsAsync([id]);
        var roles = await _userRepository.GetRolesByUserIdsAsync([id]);

        return MapToDto(user, extensions, orgUnits, roles);
    }

    public async Task DeleteAsync(Guid id)
    {
        var extension = await _identityUserExtensionRepository.FindAsync(e => e.UserId == id);
        if (extension != null)
        {
            await _identityUserExtensionRepository.DeleteAsync(extension);
        }

        var user = await _identityUserManager.GetByIdAsync(id);
        await _identityUserManager.DeleteAsync(user);
    }

    public async Task ChangeActiveAsync(Guid id, bool isActive)
    {
        var user = await _identityUserManager.GetByIdAsync(id);
        user.SetIsActive(isActive);
        await _identityUserManager.UpdateAsync(user);
    }

    public async Task ResetPasswordAsync(Guid id, string password)
    {
        if (password.Length < 8 ||
            !password.Any(char.IsLetter) ||
            !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
        {
            throw new BusinessException("DredgeAIBase:PasswordTooWeak");
        }

        var user = await _identityUserManager.GetByIdAsync(id);

        if (await _identityUserManager.HasPasswordAsync(user))
        {
            await _identityUserManager.RemovePasswordAsync(user);
        }

        var result = await _identityUserManager.AddPasswordAsync(user, password);
        if (!result.Succeeded)
        {
            throw new BusinessException("DredgeAIBase:PasswordTooWeak")
                .WithData("Errors", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<bool> CheckPhoneAsync(string phoneNumber, Guid? excludeId = null)
    {
        var existing = await _userRepository.FindByPhoneNumberAsync(phoneNumber);
        if (existing == null)
        {
            return true;
        }

        if (excludeId.HasValue && existing.Id == excludeId.Value)
        {
            return true;
        }

        return false;
    }

    private static UserDto MapToDto(
        IdentityUser user,
        List<IdentityUserExtension> extensions,
        Dictionary<Guid, List<OrganizationUnit>> orgUnits,
        Dictionary<Guid, List<string>> roles)
    {
        var extension = extensions.FirstOrDefault(e => e.UserId == user.Id);
        orgUnits.TryGetValue(user.Id, out var userOrgUnits);
        roles.TryGetValue(user.Id, out var userRoles);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Name = user.Name ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            IsActive = user.IsActive,
            ExpireTime = extension?.ExpireTime,
            OrganizationUnits = userOrgUnits?.Select(ou => new OrganizationUnitBriefDto
            {
                Key = ou.Id,
                Name = ou.DisplayName
            }).ToList() ?? [],
            RoleNames = userRoles ?? [],
            CreationTime = user.CreationTime,
        };
    }

    /// <summary>
    /// Overload for single-user detail scenario where we already have extension and need to handle list/single differently.
    /// </summary>
    private static UserDto MapToDto(
        IdentityUser user,
        IdentityUserExtension? extension,
        Dictionary<Guid, List<OrganizationUnit>> orgUnits,
        Dictionary<Guid, List<string>> roles)
    {
        orgUnits.TryGetValue(user.Id, out var userOrgUnits);
        roles.TryGetValue(user.Id, out var userRoles);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Name = user.Name ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            IsActive = user.IsActive,
            ExpireTime = extension?.ExpireTime,
            OrganizationUnits = userOrgUnits?.Select(ou => new OrganizationUnitBriefDto
            {
                Key = ou.Id,
                Name = ou.DisplayName
            }).ToList() ?? [],
            RoleNames = userRoles ?? [],
            CreationTime = user.CreationTime,
        };
    }
}
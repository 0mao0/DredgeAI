using Volo.Abp.Identity;

namespace DredgeAI;

public interface IUserRepository : IIdentityUserRepository
{
    Task<List<IdentityUser>> GetPagedListAsync(string? keyword, bool? isActive, int skipCount, int maxResultCount, string? sorting = null, Guid? organizationUnitId = null, CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(string? keyword, bool? isActive, Guid? organizationUnitId = null, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, List<OrganizationUnit>>> GetOrganizationUnitsByUserIdsAsync(List<Guid> userIds, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, List<string>>> GetRolesByUserIdsAsync(List<Guid> userIds, CancellationToken cancellationToken = default);

    Task<List<IdentityUser>> GetPagedListByRoleAsync(
        Guid? roleId,
        string? roleName,
        string? keyword,
        bool? isActive,
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default);

    Task<long> GetCountByRoleAsync(
        Guid? roleId,
        string? roleName,
        string? keyword,
        bool? isActive,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default);

    Task<IdentityUser?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);

    Task<List<IdentityUser>> GetListByUserIdsAsync(List<Guid> userIds, CancellationToken cancellationToken = default);
}

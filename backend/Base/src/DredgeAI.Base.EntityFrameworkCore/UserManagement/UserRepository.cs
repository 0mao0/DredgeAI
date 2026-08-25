using Microsoft.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;

namespace DredgeAI;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IUserRepository),typeof(IIdentityUserRepository))]
public class UserRepository : ShiwEfCoreIdentityUserRepository, IUserRepository
{
    public UserRepository(IDbContextProvider<IIdentityDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<IdentityUser>> GetPagedListAsync(
        string? keyword,
        bool? isActive,
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        if (organizationUnitId.HasValue)
        {
            var dbContext = await GetDbContextAsync();
            var userIdsInOrg = dbContext.Set<IdentityUserOrganizationUnit>()
                .Where(uou => uou.OrganizationUnitId == organizationUnitId.Value)
                .Select(uou => uou.UserId);
            queryable = queryable.Where(x => userIdsInOrg.Contains(x.Id));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            queryable = queryable.Where(x =>
                x.UserName.Contains(keyword) ||
                x.Name.Contains(keyword) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(keyword)));
        }

        if (isActive.HasValue)
        {
            queryable = queryable.Where(x => x.IsActive == isActive.Value);
        }

        return await queryable
            .OrderBy(x => x.UserName)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<Dictionary<Guid, List<OrganizationUnit>>> GetOrganizationUnitsByUserIdsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, List<OrganizationUnit>>();
        }

        var dbContext = await GetDbContextAsync();

        var query = from uou in dbContext.Set<IdentityUserOrganizationUnit>()
                    join ou in dbContext.Set<OrganizationUnit>() on uou.OrganizationUnitId equals ou.Id
                    where userIds.Contains(uou.UserId)
                    select new { uou.UserId, OrganizationUnit = ou };

        var list = await query.ToListAsync(GetCancellationToken(cancellationToken));

        return list
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.OrganizationUnit).ToList());
    }

    public async Task<Dictionary<Guid, List<string>>> GetRolesByUserIdsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, List<string>>();
        }

        var dbContext = await GetDbContextAsync();

        var query = from ur in dbContext.Set<IdentityUserRole>()
                    join r in dbContext.Set<IdentityRole>() on ur.RoleId equals r.Id
                    where userIds.Contains(ur.UserId)
                    select new { ur.UserId, r.Name };

        var list = await query.ToListAsync(GetCancellationToken(cancellationToken));

        return list
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());
    }

    public async Task<long> GetCountAsync(
        string? keyword,
        bool? isActive,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();

        if (organizationUnitId.HasValue)
        {
            var dbContext = await GetDbContextAsync();
            var userIdsInOrg = dbContext.Set<IdentityUserOrganizationUnit>()
                .Where(uou => uou.OrganizationUnitId == organizationUnitId.Value)
                .Select(uou => uou.UserId);
            queryable = queryable.Where(x => userIdsInOrg.Contains(x.Id));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            queryable = queryable.Where(x =>
                x.UserName.Contains(keyword) ||
                x.Name.Contains(keyword) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(keyword)));
        }

        if (isActive.HasValue)
        {
            queryable = queryable.Where(x => x.IsActive == isActive.Value);
        }

        return await queryable.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<List<IdentityUser>> GetPagedListByRoleAsync(
        Guid? roleId,
        string? roleName,
        string? keyword,
        bool? isActive,
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query = from u in dbContext.Set<IdentityUser>()
                    join ur in dbContext.Set<IdentityUserRole>() on u.Id equals ur.UserId
                    join r in dbContext.Set<IdentityRole>() on ur.RoleId equals r.Id
                    select new { User = u, Role = r };

        if (roleId.HasValue)
        {
            query = query.Where(x => x.Role.Id == roleId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(roleName))
        {
            query = query.Where(x => x.Role.Name == roleName);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.User.UserName.Contains(keyword) ||
                x.User.Name.Contains(keyword) ||
                (x.User.PhoneNumber != null && x.User.PhoneNumber.Contains(keyword)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.User.IsActive == isActive.Value);
        }

        if (organizationUnitId.HasValue)
        {
            var userIdsInOrg = dbContext.Set<IdentityUserOrganizationUnit>()
                .Where(uou => uou.OrganizationUnitId == organizationUnitId.Value)
                .Select(uou => uou.UserId);
            query = query.Where(x => userIdsInOrg.Contains(x.User.Id));
        }

        return await query
            .Select(x => x.User)
            .Distinct()
            .OrderBy(x =>  x.UserName)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<long> GetCountByRoleAsync(
        Guid? roleId,
        string? roleName,
        string? keyword,
        bool? isActive,
        Guid? organizationUnitId = null,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var query = from u in dbContext.Set<IdentityUser>()
                    join ur in dbContext.Set<IdentityUserRole>() on u.Id equals ur.UserId
                    join r in dbContext.Set<IdentityRole>() on ur.RoleId equals r.Id
                    select new { User = u, Role = r };

        if (roleId.HasValue)
        {
            query = query.Where(x => x.Role.Id == roleId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(roleName))
        {
            query = query.Where(x => x.Role.Name == roleName);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.User.UserName.Contains(keyword) ||
                x.User.Name.Contains(keyword) ||
                (x.User.PhoneNumber != null && x.User.PhoneNumber.Contains(keyword)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.User.IsActive == isActive.Value);
        }

        if (organizationUnitId.HasValue)
        {
            var userIdsInOrg = dbContext.Set<IdentityUserOrganizationUnit>()
                .Where(uou => uou.OrganizationUnitId == organizationUnitId.Value)
                .Select(uou => uou.UserId);
            query = query.Where(x => userIdsInOrg.Contains(x.User.Id));
        }

        return await query
            .Select(x => x.User.Id)
            .Distinct()
            .LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public async Task<IdentityUser?> FindByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable
            .FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, GetCancellationToken(cancellationToken));
    }

    public async Task<List<IdentityUser>> GetListByUserIdsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new List<IdentityUser>();
        }

        var queryable = await GetQueryableAsync();
        return await queryable
            .Where(x => userIds.Contains(x.Id))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}

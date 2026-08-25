using Microsoft.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using DredgeAI.OrganizationManagement;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;

namespace DredgeAI.OrganizationManagement;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IMyOrganizationUnitRepository), typeof(IOrganizationUnitRepository))]
public class ShiwOrganizationUnitRepository :
    ShiwEfCoreOrganizationUnitRepository,
    IMyOrganizationUnitRepository
{
    public ShiwOrganizationUnitRepository(
        IDbContextProvider<IIdentityDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<OrganizationUnit>> GetAllListAsync()
    {
        var query = await GetQueryableAsync();
        return await query.ToListAsync();
    }

    public async Task<List<OrganizationUnit>> GetPagedListAsync(
        string? keyword, int skipCount, int maxResultCount)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(ou => ou.DisplayName.Contains(keyword));
        }

        return await query
            .OrderBy(ou => ou.DisplayName)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(string? keyword)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(ou => ou.DisplayName.Contains(keyword));
        }

        return await query.CountAsync();
    }
}

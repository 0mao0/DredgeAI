using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI;

public class IdentityUserExtensionRepository :
    EfCoreRepository<DredgeAIBaseDbContext, IdentityUserExtension, Guid>,
    IIdentityUserExtensionRepository
{
    public IdentityUserExtensionRepository(IDbContextProvider<DredgeAIBaseDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<IdentityUserExtension>> GetListByUserIdsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new List<IdentityUserExtension>();
        }

        var dbSet = await GetDbSetAsync();

        return await dbSet
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}

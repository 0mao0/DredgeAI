using Microsoft.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;

namespace DredgeAI;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IIdentityUserRepository))]
public class MyIdentityUserRepository : ShiwEfCoreIdentityUserRepository, IMyIdentityUserRepository
{
    public MyIdentityUserRepository(IDbContextProvider<IIdentityDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public async Task<IdentityUser?> FindByPhoneNumberAsync(string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber && x.PhoneNumberConfirmed,
            GetCancellationToken(cancellationToken)
        );
    }
}
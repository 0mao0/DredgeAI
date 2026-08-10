using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DredgeAI.BidCompare.Data;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

public class EntityFrameworkCoreBidCompareDbSchemaMigrator
    : IBidCompareDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreBidCompareDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolve the BidCompareDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<BidCompareDbContext>()
            .Database
            .MigrateAsync();
    }
}

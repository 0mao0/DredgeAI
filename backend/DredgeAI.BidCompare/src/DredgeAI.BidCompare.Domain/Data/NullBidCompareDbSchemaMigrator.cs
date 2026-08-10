using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Data;

/* This is used if database provider does't define
 * IBidCompareDbSchemaMigrator implementation.
 */
public class NullBidCompareDbSchemaMigrator : IBidCompareDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}

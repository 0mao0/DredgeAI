using DredgeAI.BidCompare.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(BidCompareEntityFrameworkCoreModule),
    typeof(BidCompareApplicationContractsModule)
    )]
public class BidCompareDbMigratorModule : AbpModule
{
}

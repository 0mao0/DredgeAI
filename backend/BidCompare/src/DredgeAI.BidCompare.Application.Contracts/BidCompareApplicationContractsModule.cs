using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class BidCompareApplicationContractsModule : AbpModule
{
}

using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareApplicationModule),
    typeof(BidCompareDomainTestModule)
)]
public class BidCompareApplicationTestModule : AbpModule
{

}

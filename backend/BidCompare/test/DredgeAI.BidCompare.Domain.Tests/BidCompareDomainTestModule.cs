using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareDomainModule),
    typeof(BidCompareTestBaseModule)
)]
public class BidCompareDomainTestModule : AbpModule
{

}

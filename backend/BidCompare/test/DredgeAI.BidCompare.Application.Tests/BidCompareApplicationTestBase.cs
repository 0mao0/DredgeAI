using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

public abstract class BidCompareApplicationTestBase<TStartupModule> : BidCompareTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

/* Inherit from this class for your domain layer tests. */
public abstract class BidCompareDomainTestBase<TStartupModule> : BidCompareTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

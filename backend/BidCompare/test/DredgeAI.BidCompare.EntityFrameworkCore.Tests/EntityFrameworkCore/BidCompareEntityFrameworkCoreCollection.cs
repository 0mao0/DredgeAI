using Xunit;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

[CollectionDefinition(BidCompareTestConsts.CollectionDefinitionName)]
public class BidCompareEntityFrameworkCoreCollection : ICollectionFixture<BidCompareEntityFrameworkCoreFixture>
{

}

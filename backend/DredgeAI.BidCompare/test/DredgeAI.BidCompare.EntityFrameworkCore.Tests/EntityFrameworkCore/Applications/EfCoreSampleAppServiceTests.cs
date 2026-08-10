using DredgeAI.BidCompare.Samples;
using Xunit;

namespace DredgeAI.BidCompare.EntityFrameworkCore.Applications;

[Collection(BidCompareTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<BidCompareEntityFrameworkCoreTestModule>
{

}

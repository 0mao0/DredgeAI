using DredgeAI.BidCompare.Samples;
using Xunit;

namespace DredgeAI.BidCompare.EntityFrameworkCore.Domains;

[Collection(BidCompareTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<BidCompareEntityFrameworkCoreTestModule>
{

}

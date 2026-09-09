using Xunit;

namespace DredgeAI.Gateway.EntityFrameworkCore;

[CollectionDefinition(GatewayTestConsts.CollectionDefinitionName)]
public class GatewayEntityFrameworkCoreCollection : ICollectionFixture<GatewayEntityFrameworkCoreFixture>
{

}

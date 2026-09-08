using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

/* Inherit from this class for your domain layer tests. */
public abstract class GatewayDomainTestBase<TStartupModule> : GatewayTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

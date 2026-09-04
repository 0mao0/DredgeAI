using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

/* Inherit from this class for your application layer tests. */
public abstract class GatewayApplicationTestBase<TStartupModule> : GatewayDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}

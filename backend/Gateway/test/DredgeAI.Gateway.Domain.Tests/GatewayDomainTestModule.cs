using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayDomainModule),
    typeof(GatewayTestBaseModule)
)]
public class GatewayDomainTestModule : AbpModule
{

}

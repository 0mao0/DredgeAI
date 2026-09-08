using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayDomainSharedModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class GatewayApplicationContractsModule : AbpModule
{
}

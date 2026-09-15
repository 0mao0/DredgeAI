using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class GatewayApplicationContractsModule : AbpModule
{
}

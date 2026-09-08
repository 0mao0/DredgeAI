using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class GatewayDomainModule : AbpModule
{
}

using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

[DependsOn(typeof(AbpDddDomainSharedModule))]
public class GatewayDomainSharedModule : AbpModule
{
}

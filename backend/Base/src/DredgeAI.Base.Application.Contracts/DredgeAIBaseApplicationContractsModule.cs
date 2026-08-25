using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace DredgeAI;
[DependsOn(
    typeof(DredgeAIBaseDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class DredgeAIBaseApplicationContractsModule:AbpModule
{
}
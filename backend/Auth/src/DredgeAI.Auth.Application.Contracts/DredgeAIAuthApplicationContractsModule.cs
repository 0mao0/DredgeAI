using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace DredgeAI;
[DependsOn(
    typeof(DredgeAIAuthDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class DredgeAIAuthApplicationContractsModule:AbpModule
{
}
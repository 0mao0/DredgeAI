using Volo.Abp.Domain;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(DredgeAIAuthDomainSharedModule),
    typeof(AbpIdentityDomainModule)
    )]
public class DredgeAIAuthDomainModule:AbpModule
{
}
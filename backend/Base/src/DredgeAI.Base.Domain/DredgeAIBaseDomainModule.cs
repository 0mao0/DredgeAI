using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(DredgeAIBaseDomainSharedModule)
    )]
public class DredgeAIBaseDomainModule:AbpModule
{
}
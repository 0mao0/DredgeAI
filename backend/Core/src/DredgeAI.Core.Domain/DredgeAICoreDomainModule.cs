using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(DredgeAICoreDomainSharedModule)
)]
public class DredgeAICoreDomainModule:AbpModule
{
}

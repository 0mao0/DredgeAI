using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(typeof(DredgeAIAuthTestBaseModule),
    typeof(DredgeAIAuthEntityFrameworkCoreTestModule)
)]
public class DredgeAIAuthDomainTestModule : AbpModule
{
}

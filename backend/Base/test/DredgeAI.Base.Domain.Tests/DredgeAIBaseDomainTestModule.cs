using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(typeof(DredgeAIBaseTestBaseModule),
    typeof(DredgeAIBaseEntityFrameworkCoreTestModule)
)]
public class DredgeAIBaseDomainTestModule : AbpModule
{
}

using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(typeof(DredgeAICoreTestBaseModule), typeof(DredgeAICoreDomainModule))]
public class DredgeAICoreDomainTestModule : AbpModule
{
}

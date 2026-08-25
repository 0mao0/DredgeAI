using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace DredgeAI;

public abstract class DredgeAIAuthTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}

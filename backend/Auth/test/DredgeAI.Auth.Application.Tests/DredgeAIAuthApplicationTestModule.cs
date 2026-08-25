using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIAuthTestBaseModule),
    typeof(DredgeAIAuthApplicationModule),
    typeof(DredgeAIAuthDomainTestModule)
)]
public class DredgeAIAuthApplicationTestModule : AbpModule
{
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        SeedTestData(context);
    }

    private static void SeedTestData(ApplicationInitializationContext context)
    {
        using (var scope = context.ServiceProvider.CreateScope())
        {
            // 测试数据提前初始化
        }
    }
}

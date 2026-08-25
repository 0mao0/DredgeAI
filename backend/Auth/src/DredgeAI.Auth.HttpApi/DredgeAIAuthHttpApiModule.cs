using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using DredgeAI.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIAuthApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class DredgeAIAuthHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(DredgeAIAuthHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<DredgeAIAuthResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
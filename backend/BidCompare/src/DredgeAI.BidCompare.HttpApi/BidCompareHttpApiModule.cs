using Localization.Resources.AbpUi;
using DredgeAI.BidCompare.Localization;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
    )]
public class BidCompareHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(BidCompareHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigureLocalization();
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<BidCompareResource>()
                .AddBaseTypes(
                    typeof(AbpUiResource)
                );
        });
    }
}

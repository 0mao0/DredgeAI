using Localization.Resources.AbpUi;
using DredgeAI.BidCompare.Localization;
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

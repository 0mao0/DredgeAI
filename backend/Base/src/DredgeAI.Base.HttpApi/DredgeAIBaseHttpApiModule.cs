using Asp.Versioning.ApplicationModels;
using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using DredgeAI.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIBaseApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
    
    )]
public class DredgeAIBaseHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(DredgeAIBaseHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<DredgeAIBaseResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
        
        
   
        
    }
}
using DredgeAI.Localization;
using Volo.Abp.Domain;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI;


[DependsOn(
    typeof(AbpValidationModule),
    typeof(AbpDddDomainSharedModule)
)]
public class DredgeAICoreDomainSharedModule:AbpModule
{
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DredgeAICoreDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<DredgeAICoreResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/DredgeAICore");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("DredgeAICore", typeof(DredgeAICoreResource));
        });
    }
}

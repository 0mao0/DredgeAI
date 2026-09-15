using DredgeAI;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareDomainSharedModule),
    typeof(DredgeAICoreDomainModule),
    typeof(AbpDddDomainModule),
    typeof(AbpBackgroundJobsDomainModule)
)]
public class BidCompareDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<BidCompareDomainModule>();
        });
    }
}

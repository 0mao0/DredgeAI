using DredgeAI.BlobStoring;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(DredgeAICoreDomainSharedModule),
    typeof(AbpBlobStoringMinioModule),
    typeof(AbpBlobStoringFileSystemModule)
)]
public class DredgeAICoreDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 只改写已显式配置为 ABP 默认 provider 的容器（Core 模块先于上层模块配置，
        // 故此处采用条件式改写：未命中无副作用）。上层模块若在 Core 之后配置容器
        // （如 Base.Host 里 Shiw.File 的 FileBlobStoringMinioModule 已把 ProviderType
        // 设为 ShiwMinioBlobProvider，不匹配条件，互不干扰），消费方配置容器时应显式
        // blobConfiguration.ProviderType = typeof(DredgeMinioBlobProvider) /
        // typeof(DredgeFileSystemBlobProvider)。
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureAll((_, blobConfiguration) =>
            {
                if (blobConfiguration.ProviderType == typeof(MinioBlobProvider))
                {
                    blobConfiguration.ProviderType = typeof(DredgeMinioBlobProvider);
                }
                else if (blobConfiguration.ProviderType == typeof(FileSystemBlobProvider))
                {
                    blobConfiguration.ProviderType = typeof(DredgeFileSystemBlobProvider);
                }
            });
        });
    }
}

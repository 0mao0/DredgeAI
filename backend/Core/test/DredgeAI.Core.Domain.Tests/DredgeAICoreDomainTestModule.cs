using DredgeAI.BlobStoring;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAICoreTestBaseModule),
    typeof(DredgeAICoreDomainModule),
    typeof(AbpBlobStoringModule),
    typeof(AbpBlobStoringFileSystemModule),
    typeof(AbpBlobStoringMinioModule))]
public class DredgeAICoreDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "dredgeai-blob-container-tests");
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(c =>
            {
                c.UseFileSystem(fs => fs.BasePath = basePath);
                // ConfigureAll 在 Core 模块注册时立即执行，先于本模块的 ConfigureDefault，
                // 条件改写永不命中；按 Core 模块约定显式指定 Dredge provider。
                c.ProviderType = typeof(DredgeFileSystemBlobProvider);
            });
            // 回退路径用：Stub 不是 Minio/FileSystemBlobProvider，Core 的条件改写永不命中，与注册顺序无关。
            options.Containers.Configure("dredge-blob-fallback", c => c.ProviderType = typeof(StubBlobProvider));
        });
        Configure<BlobFileSystemSigningOptions>(o =>
        {
            o.SigningSecret = "test-secret";
            o.DownloadEndpointPath = "/api/bidcompare/storage/file";
        });
    }
}

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
}

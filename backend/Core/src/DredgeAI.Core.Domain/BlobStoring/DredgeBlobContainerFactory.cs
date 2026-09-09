using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace DredgeAI.BlobStoring;

/// <summary>
/// 替换 ABP 默认 <see cref="BlobContainerFactory"/>：
/// 容器的 provider 为 <see cref="IDredgeBlobProvider"/> 时创建 <see cref="DredgeBlobContainer"/>（暴露扩展能力），
/// 否则回退 <see cref="BlobContainerFactory.Create(string)"/>（自定义 ProviderType 容器不受影响）。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IBlobContainerFactory), typeof(BlobContainerFactory), typeof(DredgeBlobContainerFactory))]
public class DredgeBlobContainerFactory : BlobContainerFactory
{
    public DredgeBlobContainerFactory(
        IBlobContainerConfigurationProvider configurationProvider,
        ICurrentTenant currentTenant,
        ICancellationTokenProvider cancellationTokenProvider,
        IBlobProviderSelector providerSelector,
        IServiceProvider serviceProvider,
        IBlobNormalizeNamingService blobNormalizeNamingService)
        : base(
            configurationProvider,
            currentTenant,
            cancellationTokenProvider,
            providerSelector,
            serviceProvider,
            blobNormalizeNamingService)
    {
    }

    public override IBlobContainer Create(string name)
    {
        if (ProviderSelector.Get(name) is not IDredgeBlobProvider provider)
        {
            return base.Create(name);
        }

        return new DredgeBlobContainer(
            name,
            ConfigurationProvider.Get(name),
            provider,
            CurrentTenant,
            CancellationTokenProvider,
            BlobNormalizeNamingService,
            ServiceProvider);
    }
}

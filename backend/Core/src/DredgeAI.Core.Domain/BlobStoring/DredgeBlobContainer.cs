using Volo.Abp.BlobStoring;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace DredgeAI.BlobStoring;

/// <summary>
/// 类型化容器入口：消费方注入 <see cref="IDredgeBlobContainer{TContainer}"/>，
/// 内部经 <see cref="IBlobContainerFactory"/>（实际为 <see cref="DredgeBlobContainerFactory"/>）创建底层容器。
/// </summary>
public class DredgeBlobContainer<TContainer> : IDredgeBlobContainer<TContainer>
    where TContainer : class
{
    private readonly IDredgeBlobContainer _container;

    public DredgeBlobContainer(IBlobContainerFactory blobContainerFactory)
    {
        _container = blobContainerFactory.Create<TContainer>().As<IDredgeBlobContainer>();
    }

    public Task SaveAsync(string name, Stream stream, bool overrideExisting = false, CancellationToken cancellationToken = default)
        => _container.SaveAsync(name, stream, overrideExisting, cancellationToken);

    public Task SaveAsync(string name, Stream stream, string contentType, bool overrideExisting = false, CancellationToken cancellationToken = default)
        => _container.SaveAsync(name, stream, contentType, overrideExisting, cancellationToken);

    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
        => _container.DeleteAsync(name, cancellationToken);

    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        => _container.ExistsAsync(name, cancellationToken);

    public Task<Stream> GetAsync(string name, CancellationToken cancellationToken = default)
        => _container.GetAsync(name, cancellationToken);

    public Task<Stream?> GetOrNullAsync(string name, CancellationToken cancellationToken = default)
        => _container.GetOrNullAsync(name, cancellationToken);

    public Task<BlobFileInfo?> GetBlobFileInfoAsync(string name, CancellationToken cancellationToken = default)
        => _container.GetBlobFileInfoAsync(name, cancellationToken);

    public Task<Stream?> GetRangeOrNullAsync(string name, long offset, long length, CancellationToken cancellationToken = default)
        => _container.GetRangeOrNullAsync(name, offset, length, cancellationToken);

    public Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => _container.DeleteByPrefixAsync(prefix, cancellationToken);

    public Task<string?> GetDownloadUrlAsync(string name, int? expirySeconds = null, CancellationToken cancellationToken = default)
        => _container.GetDownloadUrlAsync(name, expirySeconds, cancellationToken);
}

/// <summary>
/// 扩展容器实现：4 个扩展方法复用 ABP <see cref="BlobContainer"/> 基类的
/// 租户切换 / 命名规范化 / ct 回退模式，委托给 <see cref="IDredgeBlobProvider"/>。
/// </summary>
public class DredgeBlobContainer : BlobContainer, IDredgeBlobContainer
{
    private readonly IDredgeBlobProvider _provider;

    public DredgeBlobContainer(
        string containerName,
        BlobContainerConfiguration configuration,
        IDredgeBlobProvider provider,
        ICurrentTenant currentTenant,
        ICancellationTokenProvider cancellationTokenProvider,
        IBlobNormalizeNamingService blobNormalizeNamingService,
        IServiceProvider serviceProvider)
        : base(
            containerName,
            configuration,
            provider,
            currentTenant,
            cancellationTokenProvider,
            blobNormalizeNamingService,
            serviceProvider)
    {
        _provider = provider;
    }

    public virtual async Task SaveAsync(string name, Stream stream, string contentType, bool overrideExisting = false, CancellationToken cancellationToken = default)
    {
        using (CurrentTenant.Change(GetTenantIdOrNull()))
        {
            var naming = BlobNormalizeNamingService.NormalizeNaming(Configuration, ContainerName, name);
            await _provider.SaveAsync(
                new BlobProviderSaveArgs(
                    naming.ContainerName!,
                    Configuration,
                    naming.BlobName!,
                    stream,
                    overrideExisting,
                    CancellationTokenProvider.FallbackToProvider(cancellationToken)),
                contentType);
        }
    }

    public virtual async Task<BlobFileInfo?> GetBlobFileInfoAsync(string name, CancellationToken cancellationToken = default)
    {
        using (CurrentTenant.Change(GetTenantIdOrNull()))
        {
            var naming = BlobNormalizeNamingService.NormalizeNaming(Configuration, ContainerName, name);
            return await _provider.GetStatOrNullAsync(
                new BlobProviderGetArgs(
                    naming.ContainerName!,
                    Configuration,
                    naming.BlobName!,
                    CancellationTokenProvider.FallbackToProvider(cancellationToken)));
        }
    }

    public virtual async Task<Stream?> GetRangeOrNullAsync(string name, long offset, long length, CancellationToken cancellationToken = default)
    {
        using (CurrentTenant.Change(GetTenantIdOrNull()))
        {
            var naming = BlobNormalizeNamingService.NormalizeNaming(Configuration, ContainerName, name);
            return await _provider.GetRangeOrNullAsync(
                new BlobProviderGetArgs(
                    naming.ContainerName!,
                    Configuration,
                    naming.BlobName!,
                    CancellationTokenProvider.FallbackToProvider(cancellationToken)),
                offset,
                length);
        }
    }

    public virtual async Task<int> DeleteByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        using (CurrentTenant.Change(GetTenantIdOrNull()))
        {
            var naming = BlobNormalizeNamingService.NormalizeNaming(Configuration, ContainerName, prefix);
            return await _provider.DeleteByPrefixAsync(
                new BlobProviderDeleteArgs(
                    naming.ContainerName!,
                    Configuration,
                    naming.BlobName!,
                    CancellationTokenProvider.FallbackToProvider(cancellationToken)));
        }
    }

    public virtual async Task<string?> GetDownloadUrlAsync(string name, int? expirySeconds = null, CancellationToken cancellationToken = default)
    {
        using (CurrentTenant.Change(GetTenantIdOrNull()))
        {
            var naming = BlobNormalizeNamingService.NormalizeNaming(Configuration, ContainerName, name);
            return await _provider.GetDownloadUrlAsync(
                new BlobProviderGetArgs(
                    naming.ContainerName!,
                    Configuration,
                    naming.BlobName!,
                    CancellationTokenProvider.FallbackToProvider(cancellationToken)),
                expirySeconds);
        }
    }
}

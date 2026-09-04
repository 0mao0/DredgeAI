using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Xunit;

namespace DredgeAI.BlobStoring;

[BlobContainerName("dredge-blob-test")]
public class DredgeBlobContainerTestContainer
{
}

/// <summary>非 Dredge provider 的最小实现，验证工厂回退路径。</summary>
public class StubBlobProvider : IBlobProvider, ITransientDependency
{
    public Task SaveAsync(BlobProviderSaveArgs args) => Task.CompletedTask;

    public Task<bool> DeleteAsync(BlobProviderDeleteArgs args) => Task.FromResult(false);

    public Task<bool> ExistsAsync(BlobProviderExistsArgs args) => Task.FromResult(false);

    public Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args) => Task.FromResult<Stream?>(null);
}

public class DredgeBlobContainer_Tests : DredgeAICoreDomainTestBase
{
    [Fact]
    public void Factory_IsReplaced_ByDredgeFactory()
    {
        GetRequiredService<IBlobContainerFactory>().ShouldBeOfType<DredgeBlobContainerFactory>();
    }

    [Fact]
    public void Create_TypedContainer_ReturnsDredgeContainer()
    {
        var factory = GetRequiredService<IBlobContainerFactory>();

        factory.Create("dredge-blob-test").ShouldBeAssignableTo<IDredgeBlobContainer>();
    }

    [Fact]
    public async Task TypedContainer_FileSystem_FullRoundTrip()
    {
        var container = GetRequiredService<IDredgeBlobContainer<DredgeBlobContainerTestContainer>>();
        var run = $"{Guid.NewGuid():N}";
        var blobName = $"{run}/a.bin";
        var content = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        await container.SaveAsync(blobName, new MemoryStream(content), "application/octet-stream", overrideExisting: true);

        var stat = await container.GetBlobFileInfoAsync(blobName);
        stat.ShouldNotBeNull();
        stat.ObjectName.ShouldBe(blobName);
        stat.Size.ShouldBe(100);
        stat.ContentType.ShouldBe("application/octet-stream");

        var range = await container.GetRangeOrNullAsync(blobName, 10, 20);
        range.ShouldNotBeNull();
        using (var ms = new MemoryStream())
        {
            await range.CopyToAsync(ms);
            ms.ToArray().ShouldBe(content.Skip(10).Take(20).ToArray());
        }

        (await container.GetBlobFileInfoAsync($"{run}/missing.bin")).ShouldBeNull();
        (await container.GetRangeOrNullAsync($"{run}/missing.bin", 0, 10)).ShouldBeNull();

        // 删除前取签名 URL：FileSystem provider 对已删对象返回 null。
        var url = await container.GetDownloadUrlAsync(blobName);
        url.ShouldNotBeNull();
        url.ShouldStartWith("/api/compare/storage/file?key=");
        url.ShouldContain(Uri.EscapeDataString(blobName));

        (await container.DeleteByPrefixAsync($"{run}/")).ShouldBe(1);
        (await container.GetBlobFileInfoAsync(blobName)).ShouldBeNull();
    }

    [Fact]
    public void NonGeneric_DefaultContainer_Resolves()
    {
        GetRequiredService<IDredgeBlobContainer>().ShouldNotBeNull();
    }

    [Fact]
    public void Create_NonDredgeProvider_FallsBackToBaseContainer()
    {
        var factory = GetRequiredService<IBlobContainerFactory>();

        // 精确基类型：证明回退且不实现 IDredgeBlobContainer。
        factory.Create("dredge-blob-fallback").ShouldBeOfType<BlobContainer>();
    }
}

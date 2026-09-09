using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.BlobStoring.Minio;
using Xunit;

namespace DredgeAI.BlobStoring;

public class DredgeBlobProvider_Tests : DredgeAICoreDomainTestBase
{
    private const string ContainerName = "test-container";

    // ── FileSystem：GetStatOrNullAsync ─────────────────────────────────

    [Fact]
    public async Task FileSystem_Stat_ExistingBlob_ReturnsInfo()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        var content = "hello blob"u8.ToArray();
        await SaveAsync(provider, configuration, "a.txt", content);

        var stat = await provider.GetStatOrNullAsync(GetArgs(configuration, "a.txt"));

        stat.ShouldNotBeNull();
        stat.ObjectName.ShouldBe("a.txt");
        stat.Size.ShouldBe(content.Length);
        stat.ContentType.ShouldBe("application/octet-stream");
        stat.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task FileSystem_Stat_MissingBlob_ReturnsNull()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;

        var stat = await provider.GetStatOrNullAsync(GetArgs(configuration, "missing.txt"));

        stat.ShouldBeNull();
    }

    // ── FileSystem：GetRangeOrNullAsync ────────────────────────────────

    [Fact]
    public async Task FileSystem_Range_ReturnsSlice()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        var content = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        await SaveAsync(provider, configuration, "range.bin", content);

        var stream = await provider.GetRangeOrNullAsync(GetArgs(configuration, "range.bin"), 10, 20);

        stream.ShouldNotBeNull();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().ShouldBe(content.Skip(10).Take(20).ToArray());
    }

    [Fact]
    public async Task FileSystem_Range_LengthBeyondEof_ReturnsAvailableBytes()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        var content = new byte[] { 1, 2, 3, 4, 5 };
        await SaveAsync(provider, configuration, "short.bin", content);

        var stream = await provider.GetRangeOrNullAsync(GetArgs(configuration, "short.bin"), 3, 100);

        stream.ShouldNotBeNull();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().ShouldBe(new byte[] { 4, 5 });
    }

    [Fact]
    public async Task FileSystem_Range_MissingBlob_ReturnsNull()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;

        var stream = await provider.GetRangeOrNullAsync(GetArgs(configuration, "missing.bin"), 0, 10);

        stream.ShouldBeNull();
    }

    [Fact]
    public async Task FileSystem_Range_OffsetBeyondEof_ReturnsEmptyStream()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        await SaveAsync(provider, configuration, "tiny.bin", new byte[] { 1, 2, 3 });

        var stream = await provider.GetRangeOrNullAsync(GetArgs(configuration, "tiny.bin"), 100, 10);

        stream.ShouldNotBeNull();
        stream.Length.ShouldBe(0);
    }

    [Fact]
    public async Task FileSystem_Range_InvalidArguments_Throws()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => provider.GetRangeOrNullAsync(GetArgs(configuration, "x.bin"), -1, 10));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => provider.GetRangeOrNullAsync(GetArgs(configuration, "x.bin"), 0, 0));
    }

    // ── FileSystem：DeleteByPrefixAsync ────────────────────────────────

    [Fact]
    public async Task FileSystem_DeleteByPrefix_DeletesMatchesOnly()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        await SaveAsync(provider, configuration, "a/1.txt", new byte[] { 1 });
        await SaveAsync(provider, configuration, "a/2.txt", new byte[] { 2 });
        await SaveAsync(provider, configuration, "b.txt", new byte[] { 3 });

        var deleted = await provider.DeleteByPrefixAsync(DeleteArgs(configuration, "a/"));

        deleted.ShouldBe(2);
        (await provider.GetStatOrNullAsync(GetArgs(configuration, "b.txt"))).ShouldNotBeNull();
        (await provider.GetStatOrNullAsync(GetArgs(configuration, "a/1.txt"))).ShouldBeNull();
    }

    [Fact]
    public async Task FileSystem_DeleteByPrefix_EmptyPrefix_Throws()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;

        await Should.ThrowAsync<ArgumentException>(
            () => provider.DeleteByPrefixAsync(DeleteArgs(configuration, " ")));
    }

    [Fact]
    public async Task FileSystem_DeleteByPrefix_NoMatch_ReturnsZero()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        await SaveAsync(provider, configuration, "b.txt", new byte[] { 3 });

        var deleted = await provider.DeleteByPrefixAsync(DeleteArgs(configuration, "zzz/"));

        deleted.ShouldBe(0);
    }

    // ── FileSystem：GetDownloadUrlAsync ────────────────────────────────

    [Fact]
    public async Task FileSystem_GetDownloadUrl_ExistingBlob_ReturnsSignedUrl()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        await SaveAsync(provider, configuration, "a.txt", "hello"u8.ToArray());

        var url = await provider.GetDownloadUrlAsync(GetArgs(configuration, "a.txt"));

        url.ShouldNotBeNull();
        url.ShouldStartWith("/api/bidcompare/storage/file?key=");
        url.ShouldContain("a.txt");
        url.ShouldContain("&expires=");
        url.ShouldContain("&sig=");
    }

    [Fact]
    public async Task FileSystem_GetDownloadUrl_MissingBlob_ReturnsNull()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;

        var url = await provider.GetDownloadUrlAsync(GetArgs(configuration, "missing.txt"));

        url.ShouldBeNull();
    }

    [Fact]
    public async Task FileSystem_SaveWithContentType_Roundtrips()
    {
        var (provider, configuration, dir) = CreateFileSystemFixture();
        using var _ = dir;
        var content = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        await provider.SaveAsync(
            new BlobProviderSaveArgs(ContainerName, configuration, "ct.bin", new MemoryStream(content), true),
            "image/png");

        // FileSystem 忽略 contentType，只证明该链路不破坏内容。
        var stream = await provider.GetRangeOrNullAsync(GetArgs(configuration, "ct.bin"), 0, content.Length);
        stream.ShouldNotBeNull();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().ShouldBe(content);
    }

    // ── Minio 集成（环境变量门控，未配置时静默跳过）─────────────────────

    [Fact]
    public async Task Minio_FullRoundTrip_Works()
    {
        if (!TryGetMinioConfig(out var endpoint, out var accessKey, out var secretKey))
        {
            return;
        }

        var configuration = new BlobContainerConfiguration();
        configuration.UseMinio(minio =>
        {
            minio.EndPoint = endpoint;
            minio.AccessKey = accessKey;
            minio.SecretKey = secretKey;
            minio.BucketName = "";
            minio.CreateBucketIfNotExists = true;
            minio.WithSSL = Environment.GetEnvironmentVariable("DREDGEAI_TEST_MINIO_WITHSSL") == "true";
        });

        var provider = GetRequiredService<DredgeMinioBlobProvider>();

        const string container = "dredgeai-blob-tests";
        var runId = Guid.NewGuid().ToString("N");
        // 原始 blob 名：Save 与四个方法同走 MinioBlobNameCalculator，host/tenants 前缀内部计算，无需手工拼接。
        var blobName = $"{runId}/data.bin";
        var content = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        try
        {
            // 上传（携带 ContentType，预签名下载按此返回）
            await provider.SaveAsync(
                new BlobProviderSaveArgs(container, configuration, blobName, new MemoryStream(content), true),
                "image/png");

            // stat
            var stat = await provider.GetStatOrNullAsync(new BlobProviderGetArgs(container, configuration, blobName));
            stat.ShouldNotBeNull();
            stat.Size.ShouldBe(content.Length);
            stat.ContentType.ShouldBe("image/png");

            // range 切片
            var range = await provider.GetRangeOrNullAsync(
                new BlobProviderGetArgs(container, configuration, blobName), 100, 50);
            range.ShouldNotBeNull();
            using var ms = new MemoryStream();
            await range.CopyToAsync(ms);
            ms.ToArray().ShouldBe(content.Skip(100).Take(50).ToArray());

            // 预签名下载地址
            var url = await provider.GetDownloadUrlAsync(
                new BlobProviderGetArgs(container, configuration, blobName));
            url.ShouldNotBeNull();
            url.ShouldContain("X-Amz-Signature");
            var downloaded = await new HttpClient().GetByteArrayAsync(url);
            downloaded.ShouldBe(content);

            // 前缀删除
            var deleted = await provider.DeleteByPrefixAsync(
                new BlobProviderDeleteArgs(container, configuration, $"{runId}/"));
            deleted.ShouldBe(1);
            (await provider.GetStatOrNullAsync(new BlobProviderGetArgs(container, configuration, blobName)))
                .ShouldBeNull();
        }
        finally
        {
            await provider.DeleteByPrefixAsync(
                new BlobProviderDeleteArgs(container, configuration, $"{runId}/"));
        }
    }

    // ── 辅助 ───────────────────────────────────────────────────────────

    private (DredgeFileSystemBlobProvider Provider, BlobContainerConfiguration Configuration, TempDirectory Dir)
        CreateFileSystemFixture()
    {
        var dir = new TempDirectory();
        var configuration = new BlobContainerConfiguration();
        configuration.UseFileSystem(fs => fs.BasePath = dir.Path);
        var provider = GetRequiredService<DredgeFileSystemBlobProvider>();
        return (provider, configuration, dir);
    }

    private static BlobProviderGetArgs GetArgs(BlobContainerConfiguration configuration, string blobName)
    {
        return new BlobProviderGetArgs(ContainerName, configuration, blobName);
    }

    private static BlobProviderDeleteArgs DeleteArgs(BlobContainerConfiguration configuration, string prefix)
    {
        return new BlobProviderDeleteArgs(ContainerName, configuration, prefix);
    }

    // 读写两侧同走 FilePathCalculator，路径天然一致，直接经 provider.SaveAsync 写入。
    private static Task SaveAsync(
        DredgeFileSystemBlobProvider provider,
        BlobContainerConfiguration configuration,
        string blobName,
        byte[] content)
    {
        return provider.SaveAsync(
            new BlobProviderSaveArgs(ContainerName, configuration, blobName, new MemoryStream(content), true));
    }

    private static bool TryGetMinioConfig(out string endpoint, out string accessKey, out string secretKey)
    {
        endpoint = Environment.GetEnvironmentVariable("DREDGEAI_TEST_MINIO_ENDPOINT") ?? "";
        accessKey = Environment.GetEnvironmentVariable("DREDGEAI_TEST_MINIO_ACCESSKEY") ?? "";
        secretKey = Environment.GetEnvironmentVariable("DREDGEAI_TEST_MINIO_SECRETKEY") ?? "";
        return !string.IsNullOrWhiteSpace(endpoint)
               && !string.IsNullOrWhiteSpace(accessKey)
               && !string.IsNullOrWhiteSpace(secretKey);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}

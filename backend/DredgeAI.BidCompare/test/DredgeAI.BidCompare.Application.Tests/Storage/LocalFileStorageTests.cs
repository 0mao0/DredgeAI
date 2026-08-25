using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Storage;

public class LocalFileStorageTests
{
    private static LocalFileStorage CreateStorage(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "bidcompare-storage-test", Guid.NewGuid().ToString("N"));
        return new LocalFileStorage(Options.Create(new LocalStorageOptions
        {
            RootPath = root,
            PublicBaseUrl = "https://localhost:44361",
            SigningSecret = "test-secret"
        }));
    }

    [Fact]
    public async Task Upload_Get_Exists_Delete_Roundtrip()
    {
        var storage = CreateStorage(out var root);
        try
        {
            IFileStorage facade = storage;
            var bytes = Encoding.UTF8.GetBytes("hello 标书");

            (await facade.ExistsAsync("compare/task/doc/origin.pdf")).ShouldBeFalse();

            await facade.UploadAsync("compare/task/doc/origin.pdf", new MemoryStream(bytes), "application/pdf");
            (await facade.ExistsAsync("compare/task/doc/origin.pdf")).ShouldBeTrue();

            string content;
            await using (var stream = await facade.GetAsync("compare/task/doc/origin.pdf"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync();
            }
            content.ShouldBe("hello 标书");

            var url = await facade.GetPresignedUrlAsync("compare/task/doc/origin.pdf", TimeSpan.FromHours(1));
            url.ShouldStartWith("/api/compare/storage/file?key=");
            url.ShouldContain("&sig=");

            await facade.DeleteAsync("compare/task/doc/origin.pdf");
            (await facade.ExistsAsync("compare/task/doc/origin.pdf")).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Key_Escaping_Root_Should_Throw()
    {
        var storage = CreateStorage(out var root);
        try
        {
            var ex = await Should.ThrowAsync<InvalidOperationException>(
                () => storage.UploadAsync("../escape.txt", new MemoryStream(), "text/plain"));
            ex.Message.ShouldContain("escapes root");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Missing_Key_Should_Throw_FileNotFound()
    {
        var storage = CreateStorage(out var root);
        try
        {
            await Should.ThrowAsync<FileNotFoundException>(() => storage.GetAsync("not-exist"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

using System.IO;
using System.Text;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Storage;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Storage;

public class InMemoryFileStorageTests
{
    [Fact]
    public async Task Upload_Get_Exists_Delete_Roundtrip()
    {
        IFileStorage storage = new InMemoryFileStorage();
        var bytes = Encoding.UTF8.GetBytes("hello");

        (await storage.ExistsAsync("k1")).ShouldBeFalse();

        await storage.UploadAsync("k1", new MemoryStream(bytes), "text/plain");
        (await storage.ExistsAsync("k1")).ShouldBeTrue();

        await using var stream = await storage.GetAsync("k1");
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).ShouldBe("hello");

        var url = await storage.GetPresignedUrlAsync("k1", System.TimeSpan.FromHours(1));
        url.ShouldNotBeNullOrWhiteSpace();

        await storage.DeleteAsync("k1");
        (await storage.ExistsAsync("k1")).ShouldBeFalse();
    }
}

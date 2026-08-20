using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Drafts;

public class CompareDraftAppServiceTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    private readonly ICompareDraftAppService _appService;

    public CompareDraftAppServiceTests()
    {
        _appService = GetRequiredService<ICompareDraftAppService>();
    }

    [Fact]
    public async Task UploadDocument_Should_Accept_Extension_Content_Mismatch_As_Warning()
    {
        // .docx 后缀 + Word 97-2003（.doc/OLE2）内容：不再拦截，解析链路按内容识别格式
        var doc = await _appService.UploadDocumentAsync(
            Guid.NewGuid(), DocumentRole.Bid, "投标文件港口院.docx", TestFiles.Doc(1));

        doc.FileName.ShouldBe("投标文件港口院.docx");
        doc.FileSize.ShouldBe(9); // 8 字节 OLE2 头 + 1 字节标记
    }
}

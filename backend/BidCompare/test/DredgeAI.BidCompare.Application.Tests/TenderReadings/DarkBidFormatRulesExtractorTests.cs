using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.TenderReadings;
using DredgeAI.BidCompare.TenderReadings.Extractors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.TenderReadings;

public class DarkBidFormatRulesExtractorTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    [Fact]
    public async Task Extract_Should_Expand_Cross_Page_Block_Into_One_Ref_Per_Page()
    {
        // 回归：暗标格式规则提取（如 4.2 修改与撤回）命中的跨页段落带 pageBBoxes，
        // 修复前规则提取器只取主 bbox，第 13 页续段无高亮；修复后每页生成一条溯源。
        var extractor = GetRequiredService<DarkBidFormatRulesExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"para\",\"pageIdx\":12,\"bbox\":[0.179,0.897,0.853,0.913]," +
            "\"text\":\"4.2 投标文件的修改与撤回：投标截止时间之前，投标人可对所递交的投标文件进行修改或撤回，但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封、标志（在包封上标明修改或撤回字样，并注明修改或撤回的时间）和递交。投标截止时间之后，投标人不得修改或撤回投标文件。\"," +
            "\"pageBBoxes\":[" +
            "{\"pageIdx\":12,\"bbox\":[0.179,0.897,0.853,0.913]}," +
            "{\"pageIdx\":13,\"bbox\":[0.142,0.083,0.855,0.152]}" +
            "]}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        var draft = drafts.ShouldHaveSingleItem();
        draft.SourceRefs.Count.ShouldBe(2);
        draft.SourceRefs[0].BlockId.ShouldBe("b1");
        draft.SourceRefs[0].PageIdx.ShouldBe(12);
        draft.SourceRefs[0].Bbox.ShouldBe(new[] { 0.179, 0.897, 0.853, 0.913 });
        draft.SourceRefs[1].PageIdx.ShouldBe(13);
        draft.SourceRefs[1].Bbox.ShouldBe(new[] { 0.142, 0.083, 0.855, 0.152 });
    }
}

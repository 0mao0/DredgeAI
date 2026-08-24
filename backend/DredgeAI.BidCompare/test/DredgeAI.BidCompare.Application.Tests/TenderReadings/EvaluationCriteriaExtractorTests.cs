using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.TenderReadings;
using DredgeAI.BidCompare.TenderReadings.Extractors;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.TenderReadings;

public class EvaluationCriteriaExtractorTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    [Fact]
    public async Task Extract_Should_Attach_Source_Ref_When_RawText_Lives_In_Table_Block()
    {
        // 回归：评分标准通常以表格呈现，内容在 block.table.html 而 block.text 为空；
        // 修复前溯源匹配只搜 block.text，表格命中永远为 0，字段无法 PDF 溯源。
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        var extractor = GetRequiredService<EvaluationCriteriaExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"title\",\"pageIdx\":2,\"bbox\":[0.1,0.1,0.9,0.15],\"text\":\"评标办法\"}," +
            "{\"blockId\":\"b2\",\"type\":\"table\",\"pageIdx\":3,\"bbox\":[0.08,0.2,0.92,0.7],\"text\":\"\"," +
            "\"table\":{\"html\":\"<table><tr><td>投标报价</td><td>以所有有效投标文件的投标报价的算术平均值为评标基准价</td></tr></table>\",\"imgPath\":\"\"}}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        llm.QueueResponse(
            "[{\"fieldKey\":\"price_score\",\"value\":{\"dimension\":\"投标报价\",\"score\":60}," +
            "\"rawText\":\"以所有有效投标文件的投标报价的算术平均值为评标基准价\"}]");

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        var draft = drafts.ShouldHaveSingleItem();
        draft.SourceRefs.ShouldNotBeEmpty();
        var source = draft.SourceRefs[0];
        source.BlockId.ShouldBe("b2");
        source.PageIdx.ShouldBe(3);
        source.Bbox.ShouldBe(new[] { 0.08, 0.2, 0.92, 0.7 });
    }

    [Fact]
    public async Task Extract_Should_Expand_Cross_Page_Block_Into_One_Ref_Per_Page()
    {
        // 回归：跨页段落（4.2 修改与撤回）原始产物带 pageBBoxes（第12页底部 + 第13页顶部），
        // 修复前只取主 bbox，跨页部分无高亮；修复后按每页展开一条溯源。
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        var extractor = GetRequiredService<EvaluationCriteriaExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"title\",\"pageIdx\":2,\"bbox\":[0.1,0.1,0.9,0.15],\"text\":\"投标文件\"}," +
            "{\"blockId\":\"b2\",\"type\":\"para\",\"pageIdx\":12,\"bbox\":[0.179,0.897,0.853,0.913]," +
            "\"text\":\"4.2 投标文件的修改与撤回：投标截止时间之前，投标人可对所递交的投标文件进行修改或撤回，但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封、标志（在包封上标明修改或撤回字样，并注明修改或撤回的时间）和递交。投标截止时间之后，投标人不得修改或撤回投标文件。\"," +
            "\"pageBBoxes\":[" +
            "{\"pageIdx\":12,\"bbox\":[0.179,0.897,0.853,0.913]}," +
            "{\"pageIdx\":13,\"bbox\":[0.142,0.083,0.855,0.152]}" +
            "]}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        llm.QueueResponse(
            "[{\"fieldKey\":\"reject_4_2\",\"value\":{\"clause\":\"投标文件的修改与撤回\"}," +
            "\"rawText\":\"4.2 投标文件的修改与撤回：投标截止时间之前，投标人可对所递交的投标文件进行修改或撤回\"}]");

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        var draft = drafts.ShouldHaveSingleItem();
        draft.SourceRefs.Count.ShouldBe(2);
        draft.SourceRefs[0].BlockId.ShouldBe("b2");
        draft.SourceRefs[0].PageIdx.ShouldBe(12);
        draft.SourceRefs[0].Bbox.ShouldBe(new[] { 0.179, 0.897, 0.853, 0.913 });
        draft.SourceRefs[1].PageIdx.ShouldBe(13);
        draft.SourceRefs[1].Bbox.ShouldBe(new[] { 0.142, 0.083, 0.855, 0.152 });
    }
}

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
}

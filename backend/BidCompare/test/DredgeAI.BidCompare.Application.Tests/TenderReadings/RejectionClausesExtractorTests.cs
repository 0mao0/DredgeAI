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

public class RejectionClausesExtractorTests : BidCompareApplicationTestBase<BidCompareApplicationTestModule>
{
    [Fact]
    public async Task Extract_Should_Split_Merged_Numbered_List_Into_Individual_Clauses()
    {
        // 回归：LLM 偶发把「1、2、3」编号列表合并成一条字段，
        // 修复后拆回逐条：每条独立 rejection_clause_N，并各自匹配自己的溯源块。
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        var extractor = GetRequiredService<RejectionClausesExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.1,0.9,0.12]," +
            "\"text\":\"投标文件出现下列情况之一的，将作为无效投标文件处理：\"}," +
            "{\"blockId\":\"b2\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.13,0.9,0.15]," +
            "\"text\":\"1、投标文件中的投标承诺书未加盖投标人的公章；\"}," +
            "{\"blockId\":\"b3\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.16,0.9,0.18]," +
            "\"text\":\"2、投标文件中的投标承诺书未加盖企业法定代表人（或企业法定代表人委托代理人）印章（或签字）的；\"}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        llm.QueueResponse(
            "[{\"fieldKey\":\"rejection_clause_1\",\"value\":{\"text\":\"合并条款\",\"mandatory\":true,\"category\":\"格式\"}," +
            "\"rawText\":\"投标文件出现下列情况之一的，将作为无效投标文件处理：1、投标文件中的投标承诺书未加盖投标人的公章；" +
            "2、投标文件中的投标承诺书未加盖企业法定代表人（或企业法定代表人委托代理人）印章（或签字）的；\"}]");

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        drafts.Count.ShouldBe(2);

        var first = drafts[0];
        first.FieldKey.ShouldBe("rejection_clause_1");
        first.RawText.ShouldContain("投标文件出现下列情况之一的");
        first.RawText.ShouldContain("1、投标文件中的投标承诺书未加盖投标人的公章");
        first.SourceRefs.Select(r => r.BlockId).OrderBy(x => x).ShouldBe(new[] { "b1", "b2" });

        var second = drafts[1];
        second.FieldKey.ShouldBe("rejection_clause_2");
        second.RawText.ShouldContain("2、投标文件中的投标承诺书未加盖企业法定代表人");
        second.SourceRefs.Select(r => r.BlockId).ShouldBe(new[] { "b3" });
    }

    [Fact]
    public async Task Extract_Should_Keep_Single_Clause_Untouched()
    {
        // 非编号列表的普通条款不应被拆分，语义 key 保留
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        var extractor = GetRequiredService<RejectionClausesExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"para\",\"pageIdx\":3,\"bbox\":[0.1,0.1,0.9,0.12]," +
            "\"text\":\"本项目严禁挂靠，一经核实挂靠，将被取消投标、中标资格。\"}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        llm.QueueResponse(
            "[{\"fieldKey\":\"affiliation_prohibited\",\"value\":{\"text\":\"本项目严禁挂靠\",\"mandatory\":true,\"category\":\"资质\"}," +
            "\"rawText\":\"本项目严禁挂靠，一经核实挂靠，将被取消投标、中标资格。\"}]");

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        var draft = drafts.ShouldHaveSingleItem();
        draft.FieldKey.ShouldBe("affiliation_prohibited");
        draft.SourceRefs.Select(r => r.BlockId).ShouldBe(new[] { "b1" });
    }
}

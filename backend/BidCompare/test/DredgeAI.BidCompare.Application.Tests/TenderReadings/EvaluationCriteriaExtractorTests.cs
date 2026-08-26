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

    [Fact]
    public async Task Extract_Should_Attach_One_Ref_Per_Block_When_Clause_Splits_Across_Blocks()
    {
        // 回归：废标条款（如 6.4 无效标条款）整段被拆成“引导句 + 1、2、3 编号项”多个块，
        // 修复前只按整句前缀匹配，只命中引导句所在块，编号项大段内容无法溯源；
        // 修复后按标点切语义片段，每个块都生成一条溯源。
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        var extractor = GetRequiredService<EvaluationCriteriaExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.1,0.9,0.12]," +
            "\"text\":\"投标文件出现下列情况之一的，将作为无效投标文件处理：\"}," +
            "{\"blockId\":\"b2\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.13,0.9,0.15]," +
            "\"text\":\"1、投标文件中的投标承诺书未加盖投标人的公章；\"}," +
            "{\"blockId\":\"b3\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.16,0.9,0.18]," +
            "\"text\":\"2、投标文件中的投标承诺书未加盖企业法定代表人（或企业法定代表人委托代理人）印章（或签字）的；\"}," +
            "{\"blockId\":\"b4\",\"type\":\"para\",\"pageIdx\":14,\"bbox\":[0.1,0.19,0.9,0.21]," +
            "\"text\":\"3、如投标承诺书加盖企业法定代表人委托代理人印章（或签字）的，企业法定代表人委托代理人没有合法、有效的委托书（原件）的；\"}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        llm.QueueResponse(
            "[{\"fieldKey\":\"reject_clause\",\"value\":{\"text\":\"无效投标\"}," +
            "\"rawText\":\"投标文件出现下列情况之一的，将作为无效投标文件处理：1、投标文件中的投标承诺书未加盖投标人的公章；" +
            "2、投标文件中的投标承诺书未加盖企业法定代表人（或企业法定代表人委托代理人）印章（或签字）的；" +
            "3、如投标承诺书加盖企业法定代表人委托代理人印章（或签字）的，企业法定代表人委托代理人没有合法、有效的委托书（原件）的；\"}]");

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        var draft = drafts.ShouldHaveSingleItem();
        draft.SourceRefs.Select(r => r.BlockId).OrderBy(x => x).ShouldBe(new[] { "b1", "b2", "b3", "b4" });
        draft.SourceRefs.Select(r => r.PageIdx).Distinct().ShouldBe(new[] { 14 });
    }

    [Fact]
    public async Task Extract_Should_Attach_Ref_For_Short_Table_Cell_RawText()
    {
        // 回归：技术参数如「增值税(6%)」这类短原文来自表格单元格，
        // 修复前候选长度下限把短文本全部过滤，匹配不到任何块，溯源为空；
        // 修复后保留全文候选，块内完整包含该文本即可溯源。
        var llm = (FakeLlmGateway)GetRequiredService<ILlmGateway>();
        var extractor = GetRequiredService<EvaluationCriteriaExtractor>();

        const string irJson =
            "{\"blocks\":[" +
            "{\"blockId\":\"b1\",\"type\":\"table\",\"pageIdx\":34,\"bbox\":[0.1,0.1,0.9,0.6],\"text\":\"表2 分项报价表\"," +
            "\"table\":{\"html\":\"<table><tr><td>序号</td><td>项目名称</td></tr><tr><td>3</td><td>增值税(6%)</td></tr></table>\",\"imgPath\":\"\"}}" +
            "]}";
        using var ir = JsonDocument.Parse(irJson);

        llm.QueueResponse(
            "[{\"fieldKey\":\"vat_rate\",\"value\":{\"name\":\"增值税税率\",\"requiredValue\":\"6%\",\"unit\":\"%\"}," +
            "\"rawText\":\"增值税(6%)\"}]");

        var drafts = await extractor.ExtractAsync(
            new BaselineExtractionContext(Guid.NewGuid(), ir.RootElement));

        var draft = drafts.ShouldHaveSingleItem();
        draft.SourceRefs.ShouldNotBeEmpty();
        var source = draft.SourceRefs[0];
        source.BlockId.ShouldBe("b1");
        source.PageIdx.ShouldBe(34);
        source.Bbox.ShouldBe(new[] { 0.1, 0.1, 0.9, 0.6 });
    }
}

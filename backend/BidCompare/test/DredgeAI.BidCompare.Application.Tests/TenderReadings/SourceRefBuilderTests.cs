using System.Linq;
using System.Text.Json;
using DredgeAI.BidCompare.TenderReadings.Extractors;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.TenderReadings;

public class SourceRefBuilderTests
{
    private static JsonElement Block(string json)
    {
        using var doc = JsonDocument.Parse($"{{\"blocks\":[{json}]}}");
        return doc.RootElement.GetProperty("blocks")[0].Clone();
    }

    [Fact]
    public void Table_With_Cells_Should_Expand_To_Cell_Page_And_Bbox()
    {
        // 回归：跨页表格（块主页面 5，命中单元格在第 6 页）必须按 cell.pageIdx 归属，
        // 否则前端 pdf.js 搜错页、高亮回退整表。
        var block = Block(
            "{\"blockId\":\"t1\",\"type\":\"table\",\"pageIdx\":5,\"bbox\":[0.087,0.137,0.91,0.901]," +
            "\"table\":{\"imgPath\":\"images/t.jpg\",\"cells\":[" +
            "{\"row\":0,\"col\":0,\"rowspan\":1,\"colspan\":1,\"pageIdx\":5,\"bbox\":[0.087,0.137,0.23,0.16],\"text\":\"条款号\"}," +
            "{\"row\":0,\"col\":1,\"rowspan\":1,\"colspan\":1,\"pageIdx\":6,\"bbox\":[0.426,0.623,0.91,0.677],\"text\":\"投标文件应加盖章印\"}" +
            "]}}");

        var drafts = SourceRefBuilder.ExpandPageRects(block, "投标文件应加盖章印");

        var draft = drafts.ShouldHaveSingleItem();
        draft.BlockId.ShouldBe("t1");
        draft.PageIdx.ShouldBe(6);
        draft.Bbox.ShouldBe(new[] { 0.426, 0.623, 0.91, 0.677 });
    }

    [Fact]
    public void Table_Without_Matching_Cell_Should_Fall_Back_To_Block_Bbox()
    {
        var block = Block(
            "{\"blockId\":\"t1\",\"type\":\"table\",\"pageIdx\":5,\"bbox\":[0.087,0.137,0.91,0.901]," +
            "\"table\":{\"imgPath\":\"images/t.jpg\",\"cells\":[" +
            "{\"row\":0,\"col\":0,\"rowspan\":1,\"colspan\":1,\"pageIdx\":5,\"bbox\":[0.087,0.137,0.23,0.16],\"text\":\"条款号\"}" +
            "]}}");

        var drafts = SourceRefBuilder.ExpandPageRects(block, "完全不在表里的条款");

        var draft = drafts.ShouldHaveSingleItem();
        draft.PageIdx.ShouldBe(5);
        draft.Bbox.ShouldBe(new[] { 0.087, 0.137, 0.91, 0.901 });
    }

    [Fact]
    public void Table_With_Degenerate_Cell_Bbox_Should_Fall_Back_To_Block_Bbox()
    {
        // docs-api 估算产物存在零高 bbox（如续页行），不可绘制，必须走块级降级
        var block = Block(
            "{\"blockId\":\"t1\",\"type\":\"table\",\"pageIdx\":5,\"bbox\":[0.087,0.137,0.91,0.901]," +
            "\"table\":{\"imgPath\":\"images/t.jpg\",\"cells\":[" +
            "{\"row\":0,\"col\":0,\"rowspan\":1,\"colspan\":1,\"pageIdx\":6,\"bbox\":[0.136,0.083,0.14,0.083],\"text\":\"投标文件应加盖章印\"}" +
            "]}}");

        var drafts = SourceRefBuilder.ExpandPageRects(block, "投标文件应加盖章印");

        var draft = drafts.ShouldHaveSingleItem();
        draft.PageIdx.ShouldBe(5);
        draft.Bbox.ShouldBe(new[] { 0.087, 0.137, 0.91, 0.901 });
    }

    [Fact]
    public void Table_Clause_Crossing_Cells_Should_Pick_Most_Specific_Fragment()
    {
        var block = Block(
            "{\"blockId\":\"t1\",\"type\":\"table\",\"pageIdx\":5,\"bbox\":[0.087,0.137,0.91,0.901]," +
            "\"table\":{\"imgPath\":\"images/t.jpg\",\"cells\":[" +
            "{\"row\":0,\"col\":0,\"rowspan\":1,\"colspan\":1,\"pageIdx\":6,\"bbox\":[0.1,0.3,0.4,0.4],\"text\":\"修改或撤回\"}," +
            "{\"row\":0,\"col\":1,\"rowspan\":1,\"colspan\":1,\"pageIdx\":6,\"bbox\":[0.1,0.5,0.9,0.6],\"text\":\"但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封\"}" +
            "]}}");

        var drafts = SourceRefBuilder.ExpandPageRects(
            block,
            "投标人可对所递交的投标文件进行修改或撤回，但所递交的修改或撤回通知必须按招标文件的规定进行编制、密封、标志和递交");

        var draft = drafts.ShouldHaveSingleItem();
        draft.PageIdx.ShouldBe(6);
        draft.Bbox.ShouldBe(new[] { 0.1, 0.5, 0.9, 0.6 });
    }
}

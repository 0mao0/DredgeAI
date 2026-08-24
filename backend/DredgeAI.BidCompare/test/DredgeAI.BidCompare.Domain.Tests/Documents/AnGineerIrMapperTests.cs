using System.Text.Json.Nodes;
using System.Linq;
using DredgeAI.BidCompare.Documents;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Documents;

public class AnGineerIrMapperTests
{
    [Fact]
    public void Map_Should_Produce_Internal_Ir_Per_V2_Field_Mapping()
    {
        var irJson = AnGineerIrMapper.MapToIrJson(SampleIr.ValidGraphJsonl, SampleIr.ValidMetaJson, "doc-a");

        // 与期望的内部适配 IR 深度一致（v2 §2 字段映射 + §3 类型映射）
        JsonNode.DeepEquals(JsonNode.Parse(irJson), JsonNode.Parse(SampleIr.Valid)).ShouldBeTrue();
    }

    [Fact]
    public void Map_Should_Apply_Type_Mapping_And_Block_Uid()
    {
        var irJson = AnGineerIrMapper.MapToIrJson(SampleIr.ValidGraphJsonl, SampleIr.ValidMetaJson, "doc-a");
        var node = JsonNode.Parse(irJson)!;

        // paragraph → para（v2 §3）；blockId 直接采用 block_uid（v2 §2）
        node["blocks"]![2]!["type"]!.GetValue<string>().ShouldBe("para");
        node["blocks"]![0]!["blockId"]!.GetValue<string>().ShouldBe("b0001");
        // 表格：table_html/image_path → table.html/table.imgPath
        node["blocks"]![1]!["table"]!["html"]!.GetValue<string>().ShouldContain("<table>");
        node["blocks"]![1]!["table"]!["imgPath"]!.GetValue<string>().ShouldBe("images/t1.jpg");
        // docs-api 单元格级坐标 → table.cells（含按页归属 pageIdx）
        var cells = node["blocks"]![1]!["table"]!["cells"]!.AsArray();
        cells.Count.ShouldBe(1);
        cells[0]!["pageIdx"]!.GetValue<int>().ShouldBe(1);
        cells[0]!["text"]!.GetValue<string>().ShouldBe("总价");
        cells[0]!["bbox"]!.AsArray().Select(x => x!.GetValue<double>())
            .ShouldBe(new double[] { 0.0672, 0.1188, 0.5, 0.2 });
    }

    [Fact]
    public void Map_Should_Tolerate_Missing_Source_And_Confidence()
    {
        // v2 §4：AnGIneer 补齐字段之前 source/confidence 缺省 → 映射为 null
        var jsonl = "{\"block_uid\":\"b1\",\"block_type\":\"paragraph\",\"page_idx\":0,\"plain_text\":\"正文\",\"bbox\":[0.1,0.1,0.9,0.2]}";
        var irJson = AnGineerIrMapper.MapToIrJson(jsonl, SampleIr.ValidMetaJson, "doc-a");
        var node = JsonNode.Parse(irJson)!;

        // .NET 8 JsonNode 将 JSON null 索引为 null 引用，用 ?. 兼容两种表示
        node["blocks"]![0]!["source"]?.GetValue<string?>().ShouldBeNull();
        node["blocks"]![0]!["confidence"]?.GetValue<double?>().ShouldBeNull();
    }

    [Fact]
    public void Map_Should_Normalize_Real_AnGineer_Source_Values()
    {
        // AnGIneer 实际产物 source 取值：text/ocr/formula/table/null → 内部 IR 归一化
        var jsonl = """
        {"block_uid":"b1","block_type":"paragraph","page_idx":0,"plain_text":"正文","bbox":[0.1,0.1,0.9,0.2],"source":"text","confidence":1.0}
        {"block_uid":"b2","block_type":"title","page_idx":0,"plain_text":"标题","bbox":[0.1,0.2,0.9,0.3],"source":"ocr","confidence":0.6}
        {"block_uid":"b3","block_type":"equation_interline","page_idx":0,"math_content":"x+y","bbox":[0.1,0.3,0.9,0.4],"source":"formula","confidence":1.0}
        {"block_uid":"b4","block_type":"table","page_idx":0,"plain_text":"表","bbox":[0.1,0.4,0.9,0.5],"table_html":"<table></table>","image_path":"images/t.jpg","source":"table","confidence":1.0}
        {"block_uid":"b5","block_type":"image","page_idx":0,"bbox":[0.1,0.5,0.9,0.6],"image_path":"images/i.jpg","source":null,"confidence":1.0}
        """;
        var irJson = AnGineerIrMapper.MapToIrJson(jsonl, SampleIr.ValidMetaJson, "doc-a");
        var node = JsonNode.Parse(irJson)!;

        node["blocks"]![0]!["source"]!.GetValue<string>().ShouldBe("native");
        node["blocks"]![1]!["source"]!.GetValue<string>().ShouldBe("ocr");
        node["blocks"]![2]!["source"]!.GetValue<string>().ShouldBe("ocr");
        node["blocks"]![3]!["source"]!.GetValue<string>().ShouldBe("native");
        node["blocks"]![4]!["source"]?.GetValue<string?>().ShouldBeNull();
    }

    [Fact]
    public void Map_Should_Preserve_Cross_Page_BBoxes_And_Merged_From()
    {
        // 回归：跨页段落/表格在原始产物中有 page_bboxes + merged_from，
        // 内部 IR 必须原样保留，否则溯源只能高亮主页面的一小条。
        var irJson = AnGineerIrMapper.MapToIrJson(SampleIr.ValidGraphJsonl, SampleIr.ValidMetaJson, "doc-a");
        var node = JsonNode.Parse(irJson)!;

        var block = node["blocks"]![1]!;
        var pageBBoxes = block["pageBBoxes"]!.AsArray();
        pageBBoxes.Count.ShouldBe(2);
        pageBBoxes[0]!["pageIdx"]!.GetValue<int>().ShouldBe(1);
        pageBBoxes[0]!["bbox"]!.AsArray().Select(x => x!.GetValue<double>())
            .ShouldBe(new double[] { 0.0672, 0.1188, 0.9244, 0.2969 });
        pageBBoxes[1]!["pageIdx"]!.GetValue<int>().ShouldBe(2);
        block["mergedFrom"]!.AsArray().Select(x => x!.GetValue<string>()).ShouldBe(new[] { "b0002-p2" });

        // 无 page_bboxes 的块不应输出空数组，保持 IR 精简
        node["blocks"]![0]!["pageBBoxes"]?.ShouldBeNull();
    }
}

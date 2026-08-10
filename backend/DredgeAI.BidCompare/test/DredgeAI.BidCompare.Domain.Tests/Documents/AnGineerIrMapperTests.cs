using System.Text.Json.Nodes;
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
}

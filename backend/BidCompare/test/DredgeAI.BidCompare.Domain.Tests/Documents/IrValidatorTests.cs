using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.Documents;

public class IrValidatorTests
{
    private readonly IrValidator _validator = new();

    [Fact]
    public void Valid_Sample_Should_Pass()
    {
        var result = _validator.Validate(SampleIr.Valid);
        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Invalid_Json_Should_Fail()
    {
        _validator.Validate("{oops").IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Missing_Required_Fields_Should_Fail()
    {
        var result = _validator.Validate("""{"schemaVersion":"1.0"}""");
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("docId"));
        result.Errors.ShouldContain(e => e.Contains("meta"));
        result.Errors.ShouldContain(e => e.Contains("blocks"));
    }

    [Fact]
    public void Pixel_Bbox_Should_Be_Rejected()
    {
        var ir = SampleIr.Valid.Replace("[0.0672, 0.0594, 0.9244, 0.095]", "[80, 100, 1100, 160]");
        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("bbox"));
    }

    [Fact]
    public void Bbox_Above_One_Should_Fail()
    {
        var ir = SampleIr.Valid.Replace("[0.0672, 0.0594, 0.9244, 0.095]", "[0, 0, 1.5, 0.095]");
        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("bbox"));
    }

    [Fact]
    public void Null_Source_And_Confidence_Should_Pass()
    {
        var ir = SampleIr.Valid
            .Replace(", \"source\": \"native\", \"confidence\": 1.0", "")
            .Replace(", \"source\": \"ocr\", \"confidence\": 0.3", "");
        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Duplicate_BlockId_Should_Fail()
    {
        var ir = SampleIr.Valid.Replace("\"blockId\": \"b0003\"", "\"blockId\": \"b0002\"");
        _validator.Validate(ir).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Table_Without_Html_And_Screenshot_Should_Fail()
    {
        var doc = JsonNode.Parse(SampleIr.Valid)!.AsObject();
        var table = (JsonObject?)doc["blocks"]![1]!["table"];
        table!.Remove("html");
        table.Remove("imgPath");
        var ir = doc.ToJsonString();

        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("table.imgPath"));
    }

    [Fact]
    public void Table_Without_Html_But_With_Screenshot_Should_Pass()
    {
        var doc = JsonNode.Parse(SampleIr.Valid)!.AsObject();
        var table = (JsonObject?)doc["blocks"]![1]!["table"];
        table!.Remove("html");
        var ir = doc.ToJsonString();

        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Missing_Bbox_Should_Be_Allowed()
    {
        var doc = JsonNode.Parse(SampleIr.Valid)!.AsObject();
        foreach (var block in (JsonArray)doc["blocks"]!)
        {
            ((JsonObject)block!).Remove("bbox");
        }
        var ir = doc.ToJsonString();

        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Confidence_Out_Of_Range_Should_Fail()
    {
        var ir = SampleIr.Valid.Replace("\"confidence\": 1.0", "\"confidence\": 1.7");
        _validator.Validate(ir).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Ocr_LowConfidence_Ratio_Should_Be_Calculated()
    {
        using var doc = JsonDocument.Parse(SampleIr.Valid);
        var ratio = IrValidator.CalculateOcrLowConfidenceRatio(doc.RootElement);
        ratio.ShouldBe(1.0 / 3.0, 0.001);
    }
}

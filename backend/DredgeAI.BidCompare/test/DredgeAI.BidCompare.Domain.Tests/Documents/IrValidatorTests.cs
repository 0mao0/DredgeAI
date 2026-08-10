using System.Text.Json;
using DredgeAI.BidCompare.Documents;
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
    public void Pixel_Bbox_Should_Be_Rejected() // v2 §2：bbox 为 0~1 归一化坐标，像素坐标拒收
    {
        var ir = SampleIr.Valid.Replace("[0.0672, 0.0594, 0.9244, 0.095]", "[80, 100, 1100, 160]");
        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("归一化"));
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
    public void Null_Source_And_Confidence_Should_Pass() // v2 §4：AnGIneer 补齐前允许缺省/null
    {
        var ir = SampleIr.Valid
            .Replace(", \"source\": \"native\", \"confidence\": 1.0", "")
            .Replace(", \"source\": \"ocr\", \"confidence\": 0.3", "");
        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Duplicate_BlockId_Should_Fail() // v2 §2：文档内唯一（= block_uid）
    {
        var ir = SampleIr.Valid.Replace("\"blockId\": \"b0003\"", "\"blockId\": \"b0002\"");
        _validator.Validate(ir).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Table_Without_Html_Or_Screenshot_Should_Fail() // spec §4.3-4
    {
        var ir = SampleIr.Valid.Replace(
            "\"table\": { \"html\": \"<table><tr><td>总价</td></tr></table>\", \"imgPath\": \"images/t1.jpg\" }",
            "\"table\": { \"html\": \"\", \"imgPath\": \"\" }");
        var result = _validator.Validate(ir);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("table.html"));
    }

    [Fact]
    public void Confidence_Out_Of_Range_Should_Fail() // v2 §4：存在时须在 0~1
    {
        var ir = SampleIr.Valid.Replace("\"confidence\": 1.0", "\"confidence\": 1.7");
        _validator.Validate(ir).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Ocr_LowConfidence_Ratio_Should_Be_Calculated() // spec §4.5
    {
        using var doc = JsonDocument.Parse(SampleIr.Valid);
        var ratio = IrValidator.CalculateOcrLowConfidenceRatio(doc.RootElement);
        ratio.ShouldBe(1.0 / 3.0, 0.001);
    }
}

using System.Linq;
using System.Reflection;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare;

public class BidCompareErrorCodesTests
{
    [Fact]
    public void All_Error_Codes_Should_Start_With_Namespace_And_Be_Unique()
    {
        var values = typeof(BidCompareErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(BidCompareErrorCodes.Namespace))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        values.ShouldNotBeEmpty();
        values.ShouldAllBe(v => v.StartsWith(BidCompareErrorCodes.Namespace));
        values.Distinct().Count().ShouldBe(values.Count);
    }
}

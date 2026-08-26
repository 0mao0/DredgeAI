using DredgeAI.BidCompare.TenderReadings;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.TenderReadings;

public class BaselineFieldKeysTests
{
    [Theory]
    [InlineData(BaselineCategory.RejectionClauses, "bid_security", 1, "bid_security")]
    [InlineData(BaselineCategory.RejectionClauses, "acceptance_payment_non_compliance", 7, "rejection_clause_7")]
    [InlineData(BaselineCategory.RejectionClauses, "", 2, "rejection_clause_2")]
    [InlineData(BaselineCategory.RejectionClauses, null, 3, "rejection_clause_3")]
    [InlineData(BaselineCategory.EvaluationCriteria, "price_score", 1, "price_score")]
    [InlineData(BaselineCategory.EvaluationCriteria, "custom_made_up_key", 3, "evaluation_criteria_3")]
    [InlineData(BaselineCategory.TechnicalParameters, "design_standard", 5, "design_standard")]
    [InlineData(BaselineCategory.TechnicalParameters, "anything_new", 4, "technical_parameter_4")]
    public void Normalize_Should_Keep_Allowed_Key_Or_Number_Fallback(
        BaselineCategory category,
        string? fieldKey,
        int index,
        string expected)
    {
        BaselineFieldKeys.Normalize(category, fieldKey, index).ShouldBe(expected);
    }
}

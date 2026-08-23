using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>
/// 基准库字段 key 词表与规范化。
/// LLM 抽取器不再允许自由发明 snake_case 英文 key（历史上曾产出 300+ 种不一致 key，
/// 前端被迫逐词翻译、每来一份新文档都要补词），统一收敛到固定词表：
/// 词表内保留语义 key（前端有中文标签），词表外一律按类别编号（如 rejection_clause_1）。
/// </summary>
public static class BaselineFieldKeys
{
    /// <summary>允许保留的语义 key：与前端 FIELD_LABELS 展示词表对齐，确保任意 key 都有中文标签。</summary>
    public static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "name",
        "code",
        "price_ceiling",
        "construction_period",
        "warranty_period",
        "payment_method",
        "outline",
        "seal_rules",
        "dark_bid_format_rules",
        "bid_price",
        "demand_understanding",
        "key_personnel",
        "management_and_safeguard_measures",
        "price_score",
        "project_manager",
        "project_team_configuration",
        "schedule_plan",
        "service_implementation_plan",
        "similar_project_performance",
        "special_services_and_commitments",
        "technical_solution",
        "acceptance_payment_terms",
        "affiliation_prohibited",
        "bid_document_sealing",
        "bid_opening_attendance_id",
        "bid_plagiarism",
        "bid_price_below_cost",
        "bid_price_exceeds_budget",
        "bid_price_exceeds_limit",
        "bid_security",
        "bid_security_deposit",
        "bid_validity_period",
        "bidder_attendance_id",
        "clarification_failure",
        "commitment_letter_seal",
        "commitment_letter_signature",
        "conditional_bidding",
        "conflict_of_interest_designer",
        "credit_disqualification",
        "excessive_missing_items",
        "failure_to_meet_qualification_or_substantive_requirements",
        "failure_to_sign_in_or_decrypt",
        "fake_materials",
        "fraud_and_collusion",
        "inspection_standards",
        "integrity_violation",
        "invalid_bid_clarification_failure",
        "invalid_bid_committee_signature",
        "invalid_bid_competitive_costs",
        "invalid_bid_duration",
        "invalid_bid_false_materials",
        "invalid_bid_fraud",
        "invalid_bid_inspection_standards",
        "invalid_bid_law_violation",
        "invalid_bid_multiple_bids",
        "invalid_bid_opening_attendance",
        "invalid_bid_payment_terms",
        "invalid_bid_personnel_mismatch",
        "invalid_bid_price_range",
        "invalid_bid_qualification",
        "invalid_bid_scheme_requirements",
        "invalid_bid_security_deposit_missing",
        "invalid_bid_similarities",
        "invalid_bid_substantive_response",
        "invalid_bid_technical_standards",
        "joint_venture_not_accepted",
        "late_submission",
        "missing_bid_letter",
        "missing_qualification_response_table",
        "missing_seals_on_critical_docs",
        "multiple_bids",
        "no_alternative_bid",
        "no_deviation",
        "no_joint_venture",
        "no_subcontracting",
        "non_competitive_fees",
        "power_of_attorney",
        "project_manager_consistency",
        "qualification_non_compliance",
        "refusal_to_confirm_price_correction",
        "same_legal_representative_or_control",
        "scheme_requirements",
        "selective_bidding",
        "substantial_response",
        "technical_standards",
        "unreasonable_low_price",
        "bid_price_type",
        "bid_validity",
        "budget_limit",
        "credit_requirement",
        "delivery_time",
        "design_depth",
        "design_standard",
        "joint_bidding",
        "payment_advance",
        "payment_final",
        "qualification",
        "service_period"
    };

    /// <summary>规范化 LLM 输出的 fieldKey：词表内保留，词表外按类别编号，杜绝英文自由发挥。</summary>
    public static string Normalize(BaselineCategory category, string? fieldKey, int index)
    {
        var candidate = fieldKey?.Trim();
        if (!string.IsNullOrWhiteSpace(candidate) && Allowed.Contains(candidate))
        {
            return candidate;
        }

        var prefix = category switch
        {
            BaselineCategory.RejectionClauses => "rejection_clause",
            BaselineCategory.EvaluationCriteria => "evaluation_criteria",
            BaselineCategory.TechnicalParameters => "technical_parameter",
            _ => "field"
        };
        return $"{prefix}_{index}";
    }
}

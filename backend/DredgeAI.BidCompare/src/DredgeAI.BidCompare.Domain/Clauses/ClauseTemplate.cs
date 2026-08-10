using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.Clauses;

/// <summary>个人条款库模板（spec §1 条款来源之一：用户手动维护）。</summary>
public class ClauseTemplate : FullAuditedAggregateRoot<Guid>
{
    public string Text { get; private set; } = default!;

    public bool Mandatory { get; private set; }

    public string? Category { get; private set; }

    protected ClauseTemplate()
    {
    }

    public ClauseTemplate(Guid id, string text, bool mandatory, string? category) : base(id)
    {
        SetValues(text, mandatory, category);
    }

    public void Update(string text, bool mandatory, string? category)
    {
        SetValues(text, mandatory, category);
    }

    private void SetValues(string text, bool mandatory, string? category)
    {
        Text = Check.NotNullOrWhiteSpace(text, nameof(text), maxLength: 2000);
        Mandatory = mandatory;
        Category = category == null ? null : Check.NotNullOrWhiteSpace(category, nameof(category), maxLength: 64);
    }
}

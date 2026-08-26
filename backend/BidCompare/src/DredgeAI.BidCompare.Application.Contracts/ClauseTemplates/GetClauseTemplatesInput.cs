using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.ClauseTemplates;

public class GetClauseTemplatesInput : PagedAndSortedResultRequestDto
{
    public string? Keyword { get; set; }

    public string? Category { get; set; }
}

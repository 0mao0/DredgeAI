using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.CompareTasks;

public class GetCompareTasksInput : PagedAndSortedResultRequestDto
{
    public string? Name { get; set; }

    public CompareTaskStatus? Status { get; set; }
}

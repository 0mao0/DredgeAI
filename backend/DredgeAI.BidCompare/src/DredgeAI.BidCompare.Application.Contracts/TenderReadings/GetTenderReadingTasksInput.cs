using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.TenderReadings;

public class GetTenderReadingTasksInput : PagedAndSortedResultRequestDto
{
    public string? Name { get; set; }

    public TenderReadingTaskStatus? Status { get; set; }
}

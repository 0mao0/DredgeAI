using System.Collections.Generic;

namespace DredgeAI.BidCompare.TenderReadings;

public class TenderReadingOutlineNodeDto
{
    public string Title { get; set; } = default!;

    public int Level { get; set; }

    public string? BlockId { get; set; }

    public List<TenderReadingOutlineNodeDto> Children { get; set; } = new();
}

using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.TenderReadings;

public class TenderReadingBaselineDto
{
    public Guid TaskId { get; set; }

    public List<BaselineFieldDto> Fields { get; set; } = new();
}

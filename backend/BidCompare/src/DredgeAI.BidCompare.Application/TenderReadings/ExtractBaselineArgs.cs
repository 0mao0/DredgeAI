using System;

namespace DredgeAI.BidCompare.TenderReadings;

public class ExtractBaselineArgs
{
    public Guid TaskId { get; set; }

    public Guid DocumentId { get; set; }
}

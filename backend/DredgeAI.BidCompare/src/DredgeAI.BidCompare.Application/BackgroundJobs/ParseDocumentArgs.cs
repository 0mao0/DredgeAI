using System;

namespace DredgeAI.BidCompare.BackgroundJobs;

public class ParseDocumentArgs
{
    public Guid TaskId { get; set; }

    public Guid DocumentId { get; set; }
}

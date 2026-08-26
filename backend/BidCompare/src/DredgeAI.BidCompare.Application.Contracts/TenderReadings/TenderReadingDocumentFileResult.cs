using System.IO;

namespace DredgeAI.BidCompare.TenderReadings;

public class TenderReadingDocumentFileResult
{
    public Stream Content { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public string FileName { get; set; } = default!;
}

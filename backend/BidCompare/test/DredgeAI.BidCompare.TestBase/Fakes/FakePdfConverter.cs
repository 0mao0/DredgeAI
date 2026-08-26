using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Exports;

public class FakePdfConverter : IPdfConverter
{
    public byte[]? LastDocx { get; private set; }

    public Task<byte[]> ConvertToPdfAsync(byte[] docxContent, CancellationToken cancellationToken = default)
    {
        LastDocx = docxContent;
        return Task.FromResult(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake-pdf-content"));
    }
}

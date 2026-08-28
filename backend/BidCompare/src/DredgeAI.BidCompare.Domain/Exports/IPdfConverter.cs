using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Exports;

/// <summary>docx → pdf 转换抽象（生产：LibreOffice headless；测试：Fake）。</summary>
public interface IPdfConverter
{
    Task<byte[]> ConvertToPdfAsync(byte[] docxContent, CancellationToken cancellationToken = default);
}

using System.IO;

namespace DredgeAI.BidCompare.Documents;

/// <summary>
/// 文档原文下载结果（PDF Viewer 预览用）。
/// </summary>
public class CompareDocumentFileResult
{
    public Stream Content { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public string FileName { get; set; } = default!;
}

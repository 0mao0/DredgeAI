namespace DredgeAI.BlobStoring;

/// <summary>
/// Blob 对象元信息（stat 结果），对齐 Shiw.File 的 BlobFileInfo 形状。
/// </summary>
public class BlobFileInfo
{
    public string ObjectName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string? ETag { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string> MetaData { get; set; } = new();
}

namespace DredgeAI.BidCompare.Storage;

public class S3StorageOptions
{
    /// <summary>MinIO 服务地址，如 http://localhost:9000。</summary>
    public string ServiceUrl { get; set; } = "http://localhost:9000";

    public string AccessKey { get; set; } = "minioadmin";

    public string SecretKey { get; set; } = "minioadmin";

    public string Bucket { get; set; } = "bid-compare";

    /// <summary>MinIO 需要 path-style 寻址。</summary>
    public bool ForcePathStyle { get; set; } = true;
}

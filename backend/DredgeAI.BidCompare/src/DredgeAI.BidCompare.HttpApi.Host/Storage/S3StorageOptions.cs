namespace DredgeAI.BidCompare.Storage;

public class S3StorageOptions
{
    /// <summary>MinIO 服务地址，如 http://localhost:9000。</summary>
    public string ServiceUrl { get; set; } = "http://localhost:9000";

    /// <summary>S3 Access Key（生产/共享环境必须经 .env 注入，禁止使用默认值）。</summary>
    public string AccessKey { get; set; } = "";

    /// <summary>S3 Secret Key（生产/共享环境必须经 .env 注入，禁止使用默认值）。</summary>
    public string SecretKey { get; set; } = "";

    public string Bucket { get; set; } = "bid-compare";

    /// <summary>MinIO 需要 path-style 寻址。</summary>
    public bool ForcePathStyle { get; set; } = true;
}

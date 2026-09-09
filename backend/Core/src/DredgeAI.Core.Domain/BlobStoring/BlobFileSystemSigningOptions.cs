namespace DredgeAI.BlobStoring;

/// <summary>FileSystem blob 签名下载 URL 配置；由宿主按 Storage:Local 配置节绑定。</summary>
public class BlobFileSystemSigningOptions
{
    /// <summary>HMAC-SHA256 密钥；为空时 GetDownloadUrlAsync 抛 InvalidOperationException。</summary>
    public string? SigningSecret { get; set; }

    /// <summary>签名下载端点路径（应用专属路由，如 /api/bidcompare/storage/file）；为空同上抛异常。</summary>
    public string DownloadEndpointPath { get; set; } = "";

    /// <summary>未指定 expirySeconds 时的默认有效期（秒），对齐消费方现状 1 小时。</summary>
    public int DefaultExpirySeconds { get; set; } = 3600;
}

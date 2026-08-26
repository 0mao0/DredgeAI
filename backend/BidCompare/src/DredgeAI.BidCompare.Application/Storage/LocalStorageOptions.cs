namespace DredgeAI.BidCompare.Storage;

public class LocalStorageOptions
{
    /// <summary>
    /// 本地磁盘存储根目录（相对路径基于进程工作目录解析）。
    /// monorepo 约定为仓库根 data/storage，宿主启动时按 .env 所在目录自动覆盖。
    /// </summary>
    public string RootPath { get; set; } = "data/storage";

    /// <summary>下载链接使用的公开基地址（本地联调对应 HttpApi.Host 的 SelfUrl）。</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:44361";

    /// <summary>签名下载 URL 的 HMAC-SHA256 密钥（生产环境经 STORAGE_LOCAL_SIGNING_SECRET 注入）。为空时拒绝生成签名 URL。</summary>
    public string? SigningSecret { get; set; }
}

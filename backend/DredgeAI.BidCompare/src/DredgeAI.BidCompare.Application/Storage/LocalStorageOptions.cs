namespace DredgeAI.BidCompare.Storage;

public class LocalStorageOptions
{
    /// <summary>本地磁盘存储根目录（相对路径基于进程工作目录解析）。</summary>
    public string RootPath { get; set; } = "App_Data/storage";

    /// <summary>下载链接使用的公开基地址（本地联调对应 HttpApi.Host 的 SelfUrl）。</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:44361";
}

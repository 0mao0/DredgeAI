namespace DredgeAI.BidCompare.AnGineer;

public class AnGineerOptions
{
    /// <summary>AnGIneer docs-api HTTP API 基地址（v0.2.1 服务拆分后端口为 8790）。</summary>
    public string BaseUrl { get; set; } = "http://localhost:8790";

    public string? ApiKey { get; set; }
}

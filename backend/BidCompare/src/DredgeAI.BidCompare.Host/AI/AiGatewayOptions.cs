namespace DredgeAI.BidCompare.AI;

public class AiGatewayOptions
{
    /// <summary>services/ai-gateway 基地址，如 http://localhost:8200。</summary>
    public string BaseUrl { get; set; } = "http://localhost:8200";

    /// <summary>ABP -> 网关的入站令牌（X-API-Key）；空表示开发环境不校验。</summary>
    public string ApiToken { get; set; } = "";

    /// <summary>校验网关 -> ABP 用量上报的令牌（X-Gateway-Token）；空表示开发环境不校验。</summary>
    public string IngestToken { get; set; } = "";

    /// <summary>单次请求超时（秒）；流式由库的四段超时控制，此处为 HTTP 总上限。</summary>
    public int TimeoutSeconds { get; set; } = 120;
}

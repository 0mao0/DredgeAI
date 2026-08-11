namespace DredgeAI.BidCompare.AnGineer;

public class AnGineerOptions
{
    /// <summary>AnGIneer HTTP API 基地址，如 http://localhost:8789。</summary>
    public string BaseUrl { get; set; } = "http://localhost:8789";

    public string? ApiKey { get; set; }
}

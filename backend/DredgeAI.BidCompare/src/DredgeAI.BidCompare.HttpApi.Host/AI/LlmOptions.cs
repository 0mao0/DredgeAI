namespace DredgeAI.BidCompare.AI;

public class LlmOptions
{
    /// <summary>OpenAI 兼容端点（不含 /chat/completions），如 https://api.openai.com/v1。</summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1";

    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 120;
}

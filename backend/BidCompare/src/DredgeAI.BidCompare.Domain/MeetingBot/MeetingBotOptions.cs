namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>meeting-bot 服务配置（MeetingBot:BaseUrl / MeetingBot:Key）。</summary>
public class MeetingBotOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8101";

    public string? Key { get; set; }

    /// <summary>DGX 模型服务共享密钥（DGX_API_KEY），供 DgxQwenTts/DgxCosyVoice/DgxAsr 鉴权。</summary>
    public string? DgxApiKey { get; set; }

    /// <summary>DGX TTS（Qwen3，OpenAI 兼容 /audio/speech）；配置后为 TTS 最高优先级。</summary>
    public DgxTtsOptions? DgxQwenTts { get; set; }

    /// <summary>DGX ASR（OpenAI 兼容 /audio/transcriptions）；配置后 ASR 优先走 DGX，失败回退 meeting-bot。</summary>
    public DgxAsrOptions? DgxAsr { get; set; }
}

/// <summary>DGX TTS 服务配置（BaseUrl 形如 http://124.221.238.70/api/tts 或 /api/cosyvoice）。</summary>
public class DgxTtsOptions
{
    public string? BaseUrl { get; set; }

    public string? Model { get; set; }

    /// <summary>音色名；Qwen3-TTS 用 serena 等，CosyVoice3 预置 serena/aiden/ryan，默认 serena。</summary>
    public string? Voice { get; set; }
}

/// <summary>DGX ASR 服务配置（BaseUrl 形如 http://124.221.238.70/api/asr）。</summary>
public class DgxAsrOptions
{
    public string? BaseUrl { get; set; }

    public string? Model { get; set; }
}

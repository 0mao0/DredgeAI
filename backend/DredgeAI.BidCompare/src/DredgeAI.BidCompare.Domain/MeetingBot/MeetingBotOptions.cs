namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>meeting-bot 服务配置（MeetingBot:BaseUrl / MeetingBot:Key）。</summary>
public class MeetingBotOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8101";

    public string? Key { get; set; }

    /// <summary>
    /// 云端 TTS（腾讯云语音合成）。配置 SecretId/SecretKey 后 TTS 优先走云端，
    /// 未配置或云端失败时回退本地 CosyVoice。
    /// </summary>
    public CloudTtsOptions? CloudTts { get; set; }
}

/// <summary>腾讯云语音合成（TextToVoice）配置，见 https://cloud.tencent.com/document/product/1073/37995。</summary>
public class CloudTtsOptions
{
    public string? SecretId { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>可选地域，例如 ap-guangzhou；留空时不传 X-TC-Region。</summary>
    public string? Region { get; set; }

    /// <summary>音色 ID，默认 101013 智辉（新闻男声），与本地 zh-male-news 风格一致。</summary>
    public int VoiceType { get; set; } = 101013;

    /// <summary>采样率，默认 16000，与本地 CosyVoice 输出一致。</summary>
    public int SampleRate { get; set; } = 16000;

    /// <summary>语速，范围 [-2, 6]，0 为正常语速，越大越快。</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>音量，范围 [-10, 10]，0 为正常音量。</summary>
    public float Volume { get; set; } = 0f;

    /// <summary>单次请求文本上限（接口上限 150 汉字，默认留 10 字余量）。</summary>
    public int MaxTextChars { get; set; } = 140;

    /// <summary>分片合成并发数。</summary>
    public int MaxConcurrency { get; set; } = 4;
}

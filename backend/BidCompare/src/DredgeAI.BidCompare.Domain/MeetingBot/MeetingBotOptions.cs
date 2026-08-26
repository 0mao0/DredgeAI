namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>meeting-bot 服务配置（MeetingBot:BaseUrl / MeetingBot:Key）。</summary>
public class MeetingBotOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8101";

    public string? Key { get; set; }
}

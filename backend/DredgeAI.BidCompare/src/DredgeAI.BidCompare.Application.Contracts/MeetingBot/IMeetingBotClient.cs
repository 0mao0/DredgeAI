using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>meeting-bot（ASR/TTS/人脸/人数/转写）HTTP 客户端契约。</summary>
public interface IMeetingBotClient
{
    Task<string> AsrAsync(byte[] audio, CancellationToken ct = default);

    Task<byte[]> TtsAsync(string text, CancellationToken ct = default);

    Task<List<FaceMatchDto>> RecognizeAsync(byte[] image, CancellationToken ct = default);

    Task<int> CountAsync(byte[] image, CancellationToken ct = default);

    Task EnrollAsync(string workerId, string name, byte[] image, CancellationToken ct = default);

    Task<string> TranscribeAsync(byte[] audio, CancellationToken ct = default);
}

public class FaceMatchDto
{
    public string? WorkerId { get; set; }

    public string? Name { get; set; }

    public double Confidence { get; set; }

    public double[] Bbox { get; set; } = [];
}

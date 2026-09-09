using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>meeting-bot（ASR/TTS/人脸/人数/转写）HTTP 客户端契约。</summary>
public interface IMeetingBotClient
{
    Task<string> AsrAsync(byte[] audio, CancellationToken ct = default);

    /// <summary>整段合成，返回带 WAV 头的 16bit 单声道音频。</summary>
    Task<byte[]> TtsAsync(string text, CancellationToken ct = default);

    /// <summary>流式合成：原始 PCM 直通 destination；
    /// onSampleRate 在读到上游响应头时回调（供 HTTP 层把真实采样率透传给前端）。</summary>
    Task StreamTtsAsync(string text, Stream destination, CancellationToken ct = default, Action<int>? onSampleRate = null);

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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;

namespace DredgeAI.BidCompare;

/// <summary>meeting-bot 可编程 Fake：默认返回固定结果，可按测试覆写。</summary>
public class FakeMeetingBotClient : IMeetingBotClient
{
    public string AsrText { get; set; } = "今天的安全交底是什么？";

    public string TranscribeText { get; set; } = "这是模拟的会议转写文本。";

    public List<FaceMatchDto> RecognizedFaces { get; set; } = [];

    public int PeopleCount { get; set; } = 3;

    public List<(string WorkerId, string Name)> Enrolled { get; } = [];

    public Task<string> AsrAsync(byte[] audio, CancellationToken ct = default)
        => Task.FromResult(AsrText);

    public Task<byte[]> TtsAsync(string text, CancellationToken ct = default)
        => Task.FromResult(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00 });

    public Task<List<FaceMatchDto>> RecognizeAsync(byte[] image, CancellationToken ct = default)
        => Task.FromResult(RecognizedFaces);

    public Task<int> CountAsync(byte[] image, CancellationToken ct = default)
        => Task.FromResult(PeopleCount);

    public Task EnrollAsync(string workerId, string name, byte[] image, CancellationToken ct = default)
    {
        Enrolled.Add((workerId, name));
        return Task.CompletedTask;
    }

    public Task<string> TranscribeAsync(byte[] audio, CancellationToken ct = default)
        => Task.FromResult(TranscribeText);
}

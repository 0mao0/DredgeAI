using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare.MeetingBot;

public interface IMeetingRecordAppService : IApplicationService
{
    Task<MeetingRecordDto> CreateAsync(PreInfoInput input);

    Task<MeetingRecordDto> GetAsync(Guid id);

    Task<PlanParseResult> ParsePlanAsync(string planText);

    Task<List<MeetingHistoryDto>> GetHistoryAsync(int maxCount = 20);

    Task<SpeechDraftDto?> GetSpeechAsync(Guid id);

    Task<SpeechDraftDto> GenerateSpeechAsync(Guid id);

    Task<SpeechDraftDto> UpdateSpeechAsync(Guid id, string content);

    Task<MeetingRecordDto> StartAsync(Guid id);

    Task<List<AttendanceItemDto>> RecognizeAttendanceAsync(Guid id, byte[] image);

    Task<List<AttendanceItemDto>> GetAttendanceAsync(Guid id);

    Task<int> SaveUnrecognizedFacesAsync(
        Guid id,
        IReadOnlyList<(byte[] Data, double Confidence, double[] Bbox)> faces);

    Task<QaRecordDto> AskQaAsync(Guid id, string question);

    Task<byte[]> GetQaAudioAsync(Guid qaId);

    Task<byte[]> GetSpeechAudioAsync(Guid id);

    Task<bool> IsSpeechAudioCachedAsync(Guid id);

    Task SaveSpeechAudioCacheAsync(Guid id, byte[] wav);

    Task PreWarmSpeechLeadAsync(Guid id);

    Task<byte[]?> GetSpeechLeadAudioAsync(Guid id);

    Task<byte[]?> GetSpeechSegmentAudioAsync(Guid id, int index);

    Task WarmSpeechSegmentsAsync(Guid id);

    Task<bool> IsSpeechLeadAudioCachedAsync(Guid id);

    Task<string> GetSpeechLeadTextAsync(Guid id);

    Task<MeetingRecordDto> SaveRecordingAsync(Guid id, byte[] audio, string fileName);

    Task<MeetingRecordDto> CompleteAsync(Guid id);

    Task<ReportDto?> GetReportAsync(Guid id);
}

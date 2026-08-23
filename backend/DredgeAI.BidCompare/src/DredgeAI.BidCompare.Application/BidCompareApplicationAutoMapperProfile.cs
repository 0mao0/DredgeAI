using AutoMapper;

namespace DredgeAI.BidCompare;

public class BidCompareApplicationAutoMapperProfile : Profile
{
    public BidCompareApplicationAutoMapperProfile()
    {
        /* You can configure your AutoMapper mapping configuration here.
         * Alternatively, you can split your mapping configurations
         * into multiple profile classes for a better organization. */

        CreateMap<Clauses.ClauseTemplate, ClauseTemplates.ClauseTemplateDto>();
        CreateMap<AI.AiUsageRecord, AI.AiUsageRecordDto>();

        CreateMap<MeetingBot.MeetingRecord, MeetingBot.MeetingRecordDto>();
        CreateMap<MeetingBot.SpeechDraft, MeetingBot.SpeechDraftDto>();
        CreateMap<MeetingBot.AttendanceRecord, MeetingBot.AttendanceItemDto>();
        CreateMap<MeetingBot.QaRecord, MeetingBot.QaRecordDto>();
        CreateMap<MeetingBot.WorkerProfile, MeetingBot.WorkerDto>();
    }
}

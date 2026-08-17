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
    }
}

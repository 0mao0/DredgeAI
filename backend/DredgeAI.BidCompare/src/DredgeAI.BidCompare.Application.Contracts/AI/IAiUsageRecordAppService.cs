using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.AI;

public interface IAiUsageRecordAppService
{
    Task<AiUsageRecordDto> CreateAsync(CreateAiUsageRecordDto input);
    Task<PagedResultDto<AiUsageRecordDto>> GetListAsync(GetAiUsageRecordsInput input);
    Task<AiUsageStatsDto> GetStatsAsync();
    Task<UsageTimeSeriesDto> GetTimeSeriesAsync(string range, DateTime? startDate = null, DateTime? endDate = null);
}

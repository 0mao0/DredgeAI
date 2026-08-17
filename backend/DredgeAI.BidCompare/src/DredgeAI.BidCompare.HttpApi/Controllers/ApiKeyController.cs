using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Route("api/apikey")]
[Route("api/admin/apikey")]
public class ApiKeyController : AbpControllerBase
{
    private readonly IAiUsageRecordAppService _usageAppService;

    public ApiKeyController(IAiUsageRecordAppService usageAppService)
    {
        _usageAppService = usageAppService;
    }

    /// <summary>GET /api/*/apikey/usage-stats 用量汇总。</summary>
    [HttpGet("usage-stats")]
    public Task<AiUsageStatsDto> GetUsageStatsAsync()
        => _usageAppService.GetStatsAsync();

    /// <summary>GET /api/*/apikey/usage-timeseries 用量时序（range=7d|30d|this-month|last-month|custom）。</summary>
    [HttpGet("usage-timeseries")]
    public Task<UsageTimeSeriesDto> GetUsageTimeSeriesAsync(
        [FromQuery] string range,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
        => _usageAppService.GetTimeSeriesAsync(range, startDate, endDate);

    /// <summary>GET /api/*/apikey/records 调用记录（分页）。</summary>
    [HttpGet("records")]
    public Task<PagedResultDto<AiUsageRecordDto>> GetUsageRecordsAsync(
        [FromQuery] GetAiUsageRecordsInput input)
        => _usageAppService.GetListAsync(input);
}

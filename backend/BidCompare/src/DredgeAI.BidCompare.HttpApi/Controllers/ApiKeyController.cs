using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>API Key 用量接口</summary>
[Authorize]
[Route("api/bidcompare/apikey")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("API Key 用量")]
public class ApiKeyController : BidCompareController
{
    private readonly IAiUsageRecordAppService _usageAppService;

    public ApiKeyController(IAiUsageRecordAppService usageAppService)
    {
        _usageAppService = usageAppService;
    }

    /// <summary>GET /api/bidcompare/apikey/usage-stats 用量汇总</summary>
    /// <returns>用量汇总统计</returns>
    [HttpGet("usage-stats")]
    public Task<AiUsageStatsDto> GetUsageStatsAsync()
        => _usageAppService.GetStatsAsync();

    /// <summary>GET /api/bidcompare/apikey/usage-timeseries 用量时序（range=7d|30d|this-month|last-month|custom）</summary>
    /// <param name="range">时间范围：7d|30d|this-month|last-month|custom</param>
    /// <param name="startDate">自定义范围开始日期</param>
    /// <param name="endDate">自定义范围结束日期</param>
    /// <returns>用量时序数据</returns>
    [HttpGet("usage-timeseries")]
    public Task<UsageTimeSeriesDto> GetUsageTimeSeriesAsync(
        [FromQuery] string range,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
        => _usageAppService.GetTimeSeriesAsync(range, startDate, endDate);

    /// <summary>GET /api/bidcompare/apikey/records 调用记录（分页）</summary>
    /// <param name="input">查询条件</param>
    /// <returns>分页的调用记录列表</returns>
    [HttpGet("records")]
    public Task<PagedResultDto<AiUsageRecordDto>> GetUsageRecordsAsync(
        [FromQuery] GetAiUsageRecordsInput input)
        => _usageAppService.GetListAsync(input);
}

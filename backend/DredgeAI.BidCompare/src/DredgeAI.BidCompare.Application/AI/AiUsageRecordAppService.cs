using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.ObjectMapping;

namespace DredgeAI.BidCompare.AI;

[RemoteService(false)]
public class AiUsageRecordAppService : ApplicationService, IAiUsageRecordAppService
{
    private readonly IRepository<AiUsageRecord, Guid> _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IObjectMapper _objectMapper;

    public AiUsageRecordAppService(
        IRepository<AiUsageRecord, Guid> repository,
        IGuidGenerator guidGenerator,
        IObjectMapper objectMapper)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _objectMapper = objectMapper;
    }

    public async Task<AiUsageRecordDto> CreateAsync(CreateAiUsageRecordDto input)
    {
        var entity = new AiUsageRecord(
            _guidGenerator.Create(),
            input.Business,
            input.UsedConfig,
            input.UsedModel,
            input.InputTokens,
            input.OutputTokens,
            input.TotalTokens,
            input.FinishReason,
            input.Attempts,
            input.LatencySeconds,
            input.CircuitBreakerState,
            input.Success,
            input.ErrorType,
            input.ErrorMessage,
            input.TextPreview);
        await _repository.InsertAsync(entity, autoSave: true);
        return _objectMapper.Map<AiUsageRecord, AiUsageRecordDto>(entity);
    }

    public async Task<PagedResultDto<AiUsageRecordDto>> GetListAsync(GetAiUsageRecordsInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!string.IsNullOrWhiteSpace(input.Business), x => x.Business == input.Business)
            .WhereIf(!string.IsNullOrWhiteSpace(input.Model), x => x.UsedConfig == input.Model || x.UsedModel == input.Model)
            .WhereIf(input.Success.HasValue, x => x.Success == input.Success.Value)
            .WhereIf(input.StartDate.HasValue, x => x.CreationTime >= input.StartDate.Value)
            .WhereIf(input.EndDate.HasValue, x => x.CreationTime <= input.EndDate.Value);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var items = await AsyncExecuter.ToListAsync(
            queryable.OrderByDescending(x => x.CreationTime)
                .PageBy(input.SkipCount, input.MaxResultCount));
        return new PagedResultDto<AiUsageRecordDto>(
            totalCount,
            _objectMapper.Map<List<AiUsageRecord>, List<AiUsageRecordDto>>(items));
    }

    public async Task<AiUsageStatsDto> GetStatsAsync()
    {
        var queryable = await _repository.GetQueryableAsync();
        var items = await AsyncExecuter.ToListAsync(queryable);
        return new AiUsageStatsDto
        {
            TotalCalls = items.Count,
            TotalTokens = items.Sum(x => x.TotalTokens ?? 0)
        };
    }

    public async Task<UsageTimeSeriesDto> GetTimeSeriesAsync(
        string range,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var (from, to) = ResolveRange(range, startDate, endDate);
        var queryable = await _repository.GetQueryableAsync();
        var items = await AsyncExecuter.ToListAsync(
            queryable.Where(x => x.CreationTime >= from && x.CreationTime < to));

        var categories = new List<string>();
        for (var day = from.Date; day < to.Date; day = day.AddDays(1))
        {
            categories.Add(day.ToString("M/d"));
        }

        return new UsageTimeSeriesDto
        {
            Categories = categories,
            ByModel = Series(items, x => x.UsedModel, from, categories.Count),
            ByKey = Series(items, x => x.UsedConfig, from, categories.Count),
            ByName = Series(items, x => x.Business, from, categories.Count),
        };
    }

    private static (DateTime From, DateTime To) ResolveRange(
        string range,
        DateTime? startDate,
        DateTime? endDate)
    {
        var now = DateTime.UtcNow;
        return range switch
        {
            "30d" => (now.AddDays(-29).Date, now.AddDays(1).Date),
            "this-month" => (new DateTime(now.Year, now.Month, 1), now.AddDays(1).Date),
            "last-month" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1),
                new DateTime(now.Year, now.Month, 1)),
            "custom" when startDate.HasValue && endDate.HasValue =>
                (startDate.Value.Date, endDate.Value.Date.AddDays(1)),
            _ => (now.AddDays(-6).Date, now.AddDays(1).Date),
        };
    }

    private static List<UsageSeriesItemDto> Series(
        List<AiUsageRecord> items,
        Func<AiUsageRecord, string> groupBy,
        DateTime from,
        int days)
    {
        return items
            .GroupBy(groupBy)
            .Select(g =>
            {
                var data = new int[days];
                foreach (var item in g)
                {
                    var idx = (item.CreationTime.Date - from.Date).Days;
                    if (idx >= 0 && idx < days)
                    {
                        data[idx] += 1;
                    }
                }
                return new UsageSeriesItemDto { Name = g.Key, Data = data.ToList() };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }
}

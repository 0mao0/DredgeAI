using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>基准库存储与缓存（DB 是唯一数据源，缓存只做热读，TTL 24 小时）。</summary>
public class BaselineStore : ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(24)
    };

    private readonly IRepository<BaselineField, Guid> _fieldRepository;
    private readonly IRepository<SourceMapItem, Guid> _sourceRepository;
    private readonly IDistributedCache _cache;

    public BaselineStore(
        IRepository<BaselineField, Guid> fieldRepository,
        IRepository<SourceMapItem, Guid> sourceRepository,
        IDistributedCache cache)
    {
        _fieldRepository = fieldRepository;
        _sourceRepository = sourceRepository;
        _cache = cache;
    }

    public async Task<TenderReadingBaselineDto> GetBaselineAsync(
        Guid taskId,
        int baselineVersion,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(taskId, baselineVersion);
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            var cachedBaseline = JsonSerializer.Deserialize<TenderReadingBaselineDto>(cached, JsonOptions);
            // 旧版本可能缓存过“空基准库”，不能直接信任；只有非空缓存才命中
            if (cachedBaseline is { Fields.Count: > 0 })
            {
                return cachedBaseline;
            }
        }

        var baseline = await BuildBaselineAsync(taskId, baselineVersion, cancellationToken);
        // 不缓存空基准库：抽取过程中前端轮询可能读到空数据，缓存后会导致抽取完成仍看不到字段
        if (baseline.Fields.Count > 0)
        {
            await _cache.SetAsync(
                cacheKey,
                JsonSerializer.SerializeToUtf8Bytes(baseline, JsonOptions),
                CacheOptions,
                cancellationToken);
        }

        return baseline;
    }

    public async Task RemoveBaselineAsync(Guid taskId, int baselineVersion, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKey(taskId, baselineVersion), cancellationToken);
    }

    public async Task<List<SourceRefDto>> GetSourceRefsAsync(
        Guid fieldId,
        CancellationToken cancellationToken = default)
    {
        var sources = await _sourceRepository.GetListAsync(
            s => s.FieldId == fieldId,
            cancellationToken: cancellationToken);
        return sources
            .OrderBy(s => s.PageIdx)
            .Select(s => new SourceRefDto
            {
                FieldId = s.FieldId,
                BlockId = s.BlockId,
                PageIdx = s.PageIdx,
                Bbox = ParseBbox(s.BboxJson),
                Text = s.Text
            })
            .ToList();
    }

    private async Task<TenderReadingBaselineDto> BuildBaselineAsync(
        Guid taskId,
        int baselineVersion,
        CancellationToken cancellationToken)
    {
        var fields = await _fieldRepository.GetListAsync(
            f => f.TaskId == taskId,
            cancellationToken: cancellationToken);
        fields = fields.OrderBy(f => f.Category).ThenBy(f => f.FieldKey).ToList();

        var fieldIds = fields.Select(f => f.Id).ToList();
        var sources = await _sourceRepository.GetListAsync(
            s => fieldIds.Contains(s.FieldId),
            cancellationToken: cancellationToken);

        var fieldDtos = fields.Select(f => new BaselineFieldDto
        {
            Id = f.Id,
            TaskId = f.TaskId,
            Category = f.Category,
            FieldKey = f.FieldKey,
            ValueJson = f.ValueJson,
            RawText = f.RawText,
            Confidence = f.Confidence,
            Status = f.Status,
            Extractor = f.Extractor,
            ExtractorVersion = f.ExtractorVersion,
            SourceRefs = sources
                .Where(s => s.FieldId == f.Id)
                .OrderBy(s => s.PageIdx)
                .Select(s => new SourceRefDto
                {
                    FieldId = s.FieldId,
                    BlockId = s.BlockId,
                    PageIdx = s.PageIdx,
                    Bbox = ParseBbox(s.BboxJson),
                    Text = s.Text
                })
                .ToList()
        }).ToList();

        return new TenderReadingBaselineDto
        {
            TaskId = taskId,
            BaselineVersion = baselineVersion,
            Fields = fieldDtos
        };
    }

    private static string CacheKey(Guid taskId, int version)
        => $"tender-read:{taskId}:baseline:{version}";

    private static double[] ParseBbox(string bboxJson)
    {
        try
        {
            return JsonSerializer.Deserialize<double[]>(bboxJson) ?? Array.Empty<double>();
        }
        catch (JsonException)
        {
            return Array.Empty<double>();
        }
    }
}

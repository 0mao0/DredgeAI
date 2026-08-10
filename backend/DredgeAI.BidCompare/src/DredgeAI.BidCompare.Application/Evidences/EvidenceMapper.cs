using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DredgeAI.BidCompare.Analysis;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>EvidenceItem 实体 ⇄ DTO / AlgoEvidence 转换（JSON 负载 camelCase，与 spec §6.1 一致）。</summary>
public static class EvidenceMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static EvidenceDto ToDto(EvidenceItem entity)
    {
        return new EvidenceDto
        {
            Id = entity.Id,
            TaskId = entity.TaskId,
            Type = entity.Type,
            Severity = entity.Severity,
            DocIds = JsonSerializer.Deserialize<List<Guid>>(entity.DocIdsJson, JsonOptions) ?? new(),
            Locations = JsonSerializer.Deserialize<List<EvidenceLocationDto>>(entity.LocationsJson, JsonOptions) ?? new(),
            Metrics = entity.MetricsJson == null
                ? null
                : JsonSerializer.Deserialize<EvidenceMetricsDto>(entity.MetricsJson, JsonOptions),
            Title = entity.Title,
            Description = entity.Description,
            AiGenerated = entity.AiGenerated
        };
    }

    public static EvidenceItem ToEntity(Guid id, Guid taskId, AlgoEvidence algo)
    {
        var docIds = algo.DocIds.Select(Guid.Parse).ToList();
        var locations = algo.Locations.Select(l => new EvidenceLocationDto
        {
            DocId = Guid.Parse(l.DocId),
            BlockIds = l.BlockIds
        }).ToList();

        return new EvidenceItem(
            id,
            taskId,
            ParseEnum<EvidenceType>(algo.Type, EvidenceType.Metadata),
            ParseEnum<EvidenceSeverity>(algo.Severity, EvidenceSeverity.Low),
            JsonSerializer.Serialize(docIds, JsonOptions),
            JsonSerializer.Serialize(locations, JsonOptions),
            algo.Metrics == null ? null : JsonSerializer.Serialize(algo.Metrics, JsonOptions),
            algo.Title,
            algo.Description,
            aiGenerated: false);
    }

    public static string SerializeDocIds(IEnumerable<Guid> docIds)
        => JsonSerializer.Serialize(docIds.ToList(), JsonOptions);

    public static string SerializeLocations(IEnumerable<EvidenceLocationDto> locations)
        => JsonSerializer.Serialize(locations.ToList(), JsonOptions);

    public static List<Guid> DeserializeDocIds(string json)
        => JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? new();

    public static double? ReadSimilarity(string? metricsJson)
    {
        if (metricsJson == null)
        {
            return null;
        }
        var metrics = JsonSerializer.Deserialize<EvidenceMetricsDto>(metricsJson, JsonOptions);
        return metrics?.Similarity;
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}

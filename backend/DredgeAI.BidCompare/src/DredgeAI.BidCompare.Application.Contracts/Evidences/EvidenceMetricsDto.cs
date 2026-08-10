using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>spec §6.1 metrics: { similarity? }；JsonExtensionData 透传算法服务后续扩展指标。</summary>
public class EvidenceMetricsDto
{
    public double? Similarity { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DredgeAI.BidCompare.Evidences;

/// <summary>spec §6.1 metrics: { similarity? }；JsonExtensionData 透传算法服务后续扩展指标。</summary>
public class EvidenceMetricsDto
{
    public double? Similarity { get; set; }

    /// <summary>矩阵专用相似度（低于雷同证据阈值）：不出现在证据清单，仅用于相似度矩阵。</summary>
    public bool? MatrixOnly { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

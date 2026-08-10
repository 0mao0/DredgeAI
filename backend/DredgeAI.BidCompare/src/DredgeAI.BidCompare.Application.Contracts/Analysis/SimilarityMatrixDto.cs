using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.Analysis;

/// <summary>spec §6：两两相似度矩阵（N×N，热力图用）。DocIds 定序，Cells 为 N×N 全量（对角线 1.0）。</summary>
public class SimilarityMatrixDto
{
    public List<Guid> DocIds { get; set; } = new();

    public List<SimilarityMatrixCellDto> Cells { get; set; } = new();
}

public class SimilarityMatrixCellDto
{
    public Guid DocAId { get; set; }

    public Guid DocBId { get; set; }

    public double Similarity { get; set; }
}

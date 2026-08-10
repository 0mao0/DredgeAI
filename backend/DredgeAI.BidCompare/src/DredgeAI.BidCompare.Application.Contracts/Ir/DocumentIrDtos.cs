using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.Ir;

/// <summary>内部适配 IR 结构（camelCase 字段名；由 AnGineerIrMapper 按 v2 §2/§3 从 doc_blocks_graph 映射，前端画 bbox 用）。</summary>
public class DocumentIrDto
{
    public string SchemaVersion { get; set; } = default!;

    public string DocId { get; set; } = default!;

    public IrMetaDto Meta { get; set; } = default!;

    public List<IrPageDto> Pages { get; set; } = new();

    public List<IrOutlineNodeDto> Outline { get; set; } = new();

    public List<IrBlockDto> Blocks { get; set; } = new();
}

public class IrMetaDto
{
    public string FileName { get; set; } = default!;

    public int PageCount { get; set; }

    public string? Author { get; set; }

    public string? CreatorTool { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}

public class IrPageDto
{
    public int PageIdx { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}

public class IrOutlineNodeDto
{
    public string Title { get; set; } = default!;

    public int Level { get; set; }

    public string? BlockId { get; set; }

    public List<IrOutlineNodeDto> Children { get; set; } = new();
}

public class IrBlockDto
{
    public string BlockId { get; set; } = default!;

    public int PageIdx { get; set; }

    /// <summary>0~1 归一化坐标 [x0,y0,x1,y1]，左上角原点（v2 §2，前端 PDF_Viewer 直接还原）。</summary>
    public double[] Bbox { get; set; } = Array.Empty<double>();

    public string Type { get; set; } = default!;

    public string Text { get; set; } = default!;

    public int TextLevel { get; set; }

    /// <summary>v2 §4：AnGIneer 补齐前允许 null（OCR 降权随之降级关闭）。</summary>
    public string? Source { get; set; }

    /// <summary>v2 §4：允许 null；存在时 native 恒 1.0。</summary>
    public double? Confidence { get; set; }

    public IrTableDto? Table { get; set; }

    public string? ImgPath { get; set; }
}

public class IrTableDto
{
    public string Html { get; set; } = default!;

    public string ImgPath { get; set; } = default!;
}

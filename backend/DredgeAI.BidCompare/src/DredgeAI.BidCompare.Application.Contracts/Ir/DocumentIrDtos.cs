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

    /// <summary>PDF 元数据时间可能是 D:20251229164720+08'00' 格式，按字符串透传，避免反序列化 500。</summary>
    public string? CreatedAt { get; set; }

    public string? ModifiedAt { get; set; }
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

    /// <summary>跨页块每页归一化 bbox（[pageIdx, bbox]），docs-ui 跨页并表/文字高亮与后端溯源展开用。</summary>
    public List<IrPageBBoxDto>? PageBBoxes { get; set; }

    /// <summary>合并进本块的续块 blockId（跨页段落/表格的后续部分）。</summary>
    public List<string>? MergedFrom { get; set; }

    public IrTableDto? Table { get; set; }

    public string? ImgPath { get; set; }
}

public class IrPageBBoxDto
{
    public int PageIdx { get; set; }

    public double[] Bbox { get; set; } = Array.Empty<double>();
}

public class IrTableDto
{
    public string Html { get; set; } = default!;

    public string ImgPath { get; set; } = default!;

    /// <summary>docs-api 单元格级坐标（row/col/rowspan/colspan/pageIdx/bbox/text，bbox 0~1 归一化），前端溯源优先命中。</summary>
    public List<IrTableCellDto> Cells { get; set; } = new();
}

public class IrTableCellDto
{
    public int Row { get; set; }

    public int Col { get; set; }

    public int Rowspan { get; set; }

    public int Colspan { get; set; }

    /// <summary>跨页表格单元格按页归属，高亮时优先于表格块 pageIdx。</summary>
    public int PageIdx { get; set; }

    public double[] Bbox { get; set; } = Array.Empty<double>();

    public string Text { get; set; } = default!;
}

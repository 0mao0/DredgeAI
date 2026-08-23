using DredgeAI.BidCompare.Ir;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>读标文档解析产物（Markdown + 内部适配 IR），供前端解析对比面板消费。</summary>
public class TenderReadingParsedDocumentDto
{
    /// <summary>解析后的 Markdown 正文；若 AnGIneer 未产出 content.md，则由 IR 块重建。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>内部适配 IR：目录 / 块 / 页尺寸等，前端可据此渲染索引树与块图谱。</summary>
    public DocumentIrDto Ir { get; set; } = new();
}

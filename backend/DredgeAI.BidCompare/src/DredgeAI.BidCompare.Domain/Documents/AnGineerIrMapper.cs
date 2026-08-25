using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DredgeAI.BidCompare.Documents;

/// <summary>
/// AnGIneer 产物 → 内部适配 IR 映射（v2 文档 §2 字段映射 + §3 类型映射）。
/// 输入 doc_blocks_graph.jsonl 与 doc_blocks_graph_meta.json 的文本内容；
/// 输出内部适配 IR JSON（blockId=block_uid、bbox 0~1 归一化直收、source/confidence 可空透传）。
/// 纯静态无依赖，Domain 层单测覆盖。
/// </summary>
public static class AnGineerIrMapper
{
    // v2 §3 类型映射表；page_number 忽略（或归入 header/footer，此处按「忽略」处理）
    private static readonly Dictionary<string, string> TypeMap = new()
    {
        ["title"] = "title",
        ["paragraph"] = "para",
        ["list"] = "list",
        ["table"] = "table",
        ["equation_interline"] = "equation",
        ["image"] = "image",
        ["figure"] = "image",
        ["page_header"] = "header",
        ["page_footer"] = "footer"
    };

    public static string MapToIrJson(string graphJsonl, string metaJson, string docId)
    {
        var meta = JsonSerializer.Deserialize<MetaDoc>(metaJson) ?? new MetaDoc();

        var blocks = new List<Dictionary<string, object?>>();
        foreach (var line in graphJsonl.Split('\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
        {
            var node = JsonSerializer.Deserialize<GraphNode>(line)
                ?? throw new JsonException("doc_blocks_graph.jsonl 存在空行");
            var rawType = node.BlockType ?? "";
            if (rawType == "page_number")
            {
                continue; // v2 §3：忽略
            }
            var type = TypeMap.TryGetValue(rawType, out var mapped) ? mapped : "para";

            var block = new Dictionary<string, object?>
            {
                ["blockId"] = node.BlockUid,
                ["pageIdx"] = node.PageIdx,
                ["bbox"] = node.Bbox,
                ["type"] = type,
                ["text"] = ReadText(node, type),
                // v2 §2：标题块 textLevel=derived_level，非标题固定 0
                ["textLevel"] = type == "title" ? node.DerivedLevel ?? 1 : 0,
                ["source"] = NormalizeSource(node.Source), // v2 §4：AnGIneer 原始取值归一化为 native|ocr（text→native、formula/table→对应识别途径）
                ["confidence"] = node.Confidence
            };
            // 跨页/合并块保留每页 bbox 与合并来源：docs-ui 靠 page_bboxes 做跨页并表/文字的完整高亮，
            // 后端溯源按此展开为每页一条 SourceRef，否则跨页内容只能高亮主页面的一小条。
            if (node.PageBBoxes is { Count: > 0 })
            {
                block["pageBBoxes"] = node.PageBBoxes
                    .Where(p => p.Bbox is { Length: 4 })
                    .Select(p => new Dictionary<string, object?>
                    {
                        ["pageIdx"] = p.PageIdx,
                        ["bbox"] = p.Bbox
                    })
                    .ToList();
            }
            if (node.MergedFrom is { Count: > 0 })
            {
                block["mergedFrom"] = node.MergedFrom;
            }
            if (type == "table")
            {
                var table = new Dictionary<string, object?>
                {
                    ["html"] = node.TableHtml,
                    ["imgPath"] = node.ImagePath
                };
                if (node.TableCells is { Count: > 0 })
                {
                    // docs-api 单元格级坐标透传（bbox 0~1 归一化），供前端表格溯源命中具体单元格
                    table["cells"] = node.TableCells
                        .Where(c => c.Bbox is { Length: 4 })
                        .Select(c => new Dictionary<string, object?>
                        {
                            ["row"] = c.Row,
                            ["col"] = c.Col,
                            ["rowspan"] = c.Rowspan,
                            ["colspan"] = c.Colspan,
                            ["pageIdx"] = c.PageIdx,
                            ["bbox"] = c.Bbox,
                            ["text"] = c.Text
                        })
                        .ToList();
                }
                if (!string.IsNullOrEmpty(node.TableCellsSource))
                {
                    table["cellsSource"] = node.TableCellsSource;
                }
                block["table"] = table;
            }
            if (type is "image" or "equation" && node.ImagePath != null)
            {
                block["imgPath"] = node.ImagePath;
            }
            blocks.Add(block);
        }

        var ir = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "2.0", // 内部适配 IR 版本（1.0 为已废止的 ir.json 交付契约）
            ["docId"] = docId,
            ["meta"] = meta.DocMeta ?? new Dictionary<string, object?>(),
            ["pages"] = (meta.Pages ?? new List<MetaPage>()).Select(p => new Dictionary<string, object?>
            {
                ["pageIdx"] = p.PageIdx,
                ["width"] = p.Width,
                ["height"] = p.Height
            }).ToList(),
            ["outline"] = MapOutline(meta.Outlines),
            ["blocks"] = blocks
        };
        return JsonSerializer.Serialize(ir);
    }

    private static string? ReadText(GraphNode node, string type)
    {
        // v2 §2：公式块用 math_content / formula_body（LaTeX）
        if (type == "equation")
        {
            return node.MathContent ?? node.FormulaBody ?? node.PlainText;
        }
        return node.PlainText;
    }

    /// <summary>
    /// AnGIneer 原始 source 归一化为内部 IR 契约取值（v2 §4：native|ocr 或 null）：
    /// text → native（PDF 原生文本）；ocr → ocr；formula → ocr（公式经 OCR 识别）；
    /// table → native（表格结构化提取）；其余取值/缺省原样透传（由 IrValidator 拒收未知值）。
    /// </summary>
    private static string? NormalizeSource(string? source) => source switch
    {
        "text" => "native",
        "ocr" => "ocr",
        "formula" => "ocr",
        "table" => "native",
        _ => source
    };

    /// <summary>v2 §5-6：嵌套 outlines 直收；扁平结构（parent_outline_id）转嵌套 children。</summary>
    private static List<Dictionary<string, object?>> MapOutline(List<OutlineNode>? outlines)
    {
        if (outlines == null || outlines.Count == 0)
        {
            return new List<Dictionary<string, object?>>();
        }
        if (outlines.Any(o => o.Children is { Count: > 0 }))
        {
            return outlines.Select(ConvertOutlineNode).ToList();
        }
        if (outlines.All(o => o.ParentOutlineId == null))
        {
            return outlines.Select(ConvertOutlineNode).ToList();
        }
        var roots = outlines.Where(o => o.ParentOutlineId == null).ToList();
        return roots.Select(r => BuildOutlineNode(r, outlines)).ToList();
    }

    private static Dictionary<string, object?> ConvertOutlineNode(OutlineNode node) => new()
    {
        ["title"] = node.Title,
        ["level"] = node.Level,
        ["blockId"] = node.BlockUid ?? node.BlockId,
        ["children"] = (node.Children ?? new List<OutlineNode>()).Select(ConvertOutlineNode).ToList()
    };

    private static Dictionary<string, object?> BuildOutlineNode(OutlineNode node, List<OutlineNode> all) => new()
    {
        ["title"] = node.Title,
        ["level"] = node.Level,
        ["blockId"] = node.BlockUid ?? node.BlockId,
        ["children"] = all.Where(o => o.ParentOutlineId == node.OutlineId).Select(o => BuildOutlineNode(o, all)).ToList()
    };

    private class GraphNode
    {
        [JsonPropertyName("block_uid")] public string? BlockUid { get; set; }
        [JsonPropertyName("block_type")] public string? BlockType { get; set; }
        [JsonPropertyName("page_idx")] public int PageIdx { get; set; }
        [JsonPropertyName("plain_text")] public string? PlainText { get; set; }
        [JsonPropertyName("derived_level")] public int? DerivedLevel { get; set; }
        [JsonPropertyName("bbox")] public double[]? Bbox { get; set; }
        [JsonPropertyName("table_html")] public string? TableHtml { get; set; }
        [JsonPropertyName("math_content")] public string? MathContent { get; set; }
        [JsonPropertyName("formula_body")] public string? FormulaBody { get; set; }
        [JsonPropertyName("image_path")] public string? ImagePath { get; set; }
        [JsonPropertyName("source")] public string? Source { get; set; }
        // 保留原始数字 token（1.0 不折叠成 1），与内部适配 IR 样例一致
        [JsonPropertyName("confidence")] public JsonElement? Confidence { get; set; }
        [JsonPropertyName("page_bboxes")] public List<PageBBox>? PageBBoxes { get; set; }
        [JsonPropertyName("merged_from")] public List<string>? MergedFrom { get; set; }
        [JsonPropertyName("table_cells")] public List<GraphTableCell>? TableCells { get; set; }
        [JsonPropertyName("table_cells_source")] public string? TableCellsSource { get; set; }
    }

    private class GraphTableCell
    {
        [JsonPropertyName("row")] public int Row { get; set; }
        [JsonPropertyName("col")] public int Col { get; set; }
        [JsonPropertyName("rowspan")] public int Rowspan { get; set; }
        [JsonPropertyName("colspan")] public int Colspan { get; set; }
        [JsonPropertyName("page_idx")] public int PageIdx { get; set; }
        [JsonPropertyName("bbox")] public double[]? Bbox { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    private class PageBBox
    {
        [JsonPropertyName("page_idx")] public int PageIdx { get; set; }

        [JsonPropertyName("bbox")] public double[]? Bbox { get; set; }
    }

    private class OutlineNode
    {
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("level")] public int Level { get; set; }
        [JsonPropertyName("block_uid")] public string? BlockUid { get; set; }
        [JsonPropertyName("blockId")] public string? BlockId { get; set; }
        [JsonPropertyName("outline_id")] public string? OutlineId { get; set; }
        [JsonPropertyName("parent_outline_id")] public string? ParentOutlineId { get; set; }
        [JsonPropertyName("children")] public List<OutlineNode>? Children { get; set; }
    }

    private class MetaDoc
    {
        [JsonPropertyName("outlines")] public List<OutlineNode>? Outlines { get; set; }
        [JsonPropertyName("docMeta")] public Dictionary<string, object?>? DocMeta { get; set; }
        [JsonPropertyName("pages")] public List<MetaPage>? Pages { get; set; }
    }

    private class MetaPage
    {
        [JsonPropertyName("page_idx")] public int PageIdx { get; set; }
        [JsonPropertyName("width")] public double Width { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
    }
}

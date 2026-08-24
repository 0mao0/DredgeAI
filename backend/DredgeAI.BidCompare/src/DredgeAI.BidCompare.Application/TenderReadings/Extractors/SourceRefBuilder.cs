using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>
/// 溯源锚点构造器：把命中的 IR 块展开为 PDF 溯源锚点，
/// 规则提取器与 LLM 提取器共用，保证跨页段落/表格的溯源行为一致。
/// </summary>
public static class SourceRefBuilder
{
    /// <summary>
    /// 将命中块展开为溯源锚点：优先 pageBBoxes（跨页块每页一段），
    /// 其次 mergedBBoxes（合并块多矩形，沿用所在页），最后回退单 bbox。
    /// </summary>
    public static List<SourceMapItemDraft> ExpandPageRects(JsonElement block, string excerpt)
    {
        var blockId = GetBlockString(block, "blockId") ?? string.Empty;
        var drafts = new List<SourceMapItemDraft>();
        var seen = new HashSet<string>();

        // 表格块优先展开为单元格级溯源：命中 cell 直接取 cell.pageIdx + cell.bbox，
        // 跨页表格按单元格实际所在页归属，避免整表高亮与页码错位。
        if (TryExpandTableCell(block, excerpt, drafts, seen))
        {
            return drafts;
        }

        void Add(int pageIdx, double[] bbox)
        {
            if (bbox.Length != 4 || !seen.Add($"{pageIdx}|{string.Join(",", bbox)}"))
            {
                return;
            }

            drafts.Add(new SourceMapItemDraft
            {
                BlockId = blockId,
                PageIdx = pageIdx,
                Bbox = bbox,
                Text = excerpt
            });
        }

        var pageBBoxes = ReadPageBBoxes(block);
        if (pageBBoxes.Count > 0)
        {
            foreach (var (pageIdx, bbox) in pageBBoxes)
            {
                Add(pageIdx, bbox);
            }

            return drafts;
        }

        var mergedBBoxes = ReadMergedBBoxes(block);
        if (mergedBBoxes.Count > 0)
        {
            var pageIdx = GetBlockInt(block, "pageIdx");
            foreach (var bbox in mergedBBoxes)
            {
                Add(pageIdx, bbox);
            }

            return drafts;
        }

        Add(GetBlockInt(block, "pageIdx"), ReadBbox(block));
        return drafts;
    }

    /// <summary>
    /// 表格块命中时按单元格匹配摘录文本：优先「单元格包含整段」（取文本最短、bbox 最紧），
    /// 其次「摘录包含单元格片段」（取最长片段）。bbox 退化（零高/零宽）时返回 false 走块级降级。
    /// </summary>
    private static bool TryExpandTableCell(
        JsonElement block,
        string excerpt,
        List<SourceMapItemDraft> drafts,
        HashSet<string> seen)
    {
        if (GetBlockString(block, "type") != "table"
            || !block.TryGetProperty("table", out var table)
            || table.ValueKind != JsonValueKind.Object
            || !table.TryGetProperty("cells", out var cells)
            || cells.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var needle = NormalizeForCellMatch(excerpt);
        if (needle.Length == 0)
        {
            return false;
        }

        JsonElement? bestCell = null;
        var bestKind = int.MaxValue; // 1=整段命中（cell ⊇ needle），2=片段命中（needle ⊇ cell）
        var bestLen = 0;
        foreach (var cell in cells.EnumerateArray())
        {
            var cellText = GetBlockString(cell, "text");
            if (string.IsNullOrWhiteSpace(cellText))
            {
                continue;
            }

            var normalized = NormalizeForCellMatch(cellText);
            if (normalized.Length == 0)
            {
                continue;
            }

            int kind;
            if (normalized.Contains(needle, StringComparison.Ordinal))
            {
                kind = 1;
                if (kind < bestKind || (kind == bestKind && normalized.Length < bestLen))
                {
                    bestCell = cell;
                    bestKind = kind;
                    bestLen = normalized.Length;
                }
            }
            else if (needle.Contains(normalized, StringComparison.Ordinal))
            {
                kind = 2;
                if (kind < bestKind || (kind == bestKind && normalized.Length > bestLen))
                {
                    bestCell = cell;
                    bestKind = kind;
                    bestLen = normalized.Length;
                }
            }
        }

        if (bestCell == null)
        {
            return false;
        }

        var bbox = ReadBbox(bestCell.Value);
        var pageIdx = GetBlockInt(bestCell.Value, "pageIdx");
        if (bbox.Length != 4 || bbox[2] <= bbox[0] || bbox[3] <= bbox[1])
        {
            // docs-api 估算产物的退化矩形（如续页零高 bbox）无法绘制，交给块级降级
            return false;
        }

        if (!seen.Add($"{pageIdx}|{string.Join(",", bbox)}"))
        {
            return false;
        }

        drafts.Add(new SourceMapItemDraft
        {
            BlockId = GetBlockString(block, "blockId") ?? string.Empty,
            PageIdx = pageIdx,
            Bbox = bbox,
            Text = excerpt
        });
        return true;
    }

    /// <summary>与前端 normalizeText 一致的归一化：去空白/标点/符号，统一小写。</summary>
    private static string NormalizeForCellMatch(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }

    /// <summary>读取块 bbox 数组 [x0,y0,x1,y1]；缺失/畸形时返回空数组。</summary>
    public static double[] GetBbox(JsonElement block) => ReadBbox(block);

    public static string? GetBlockId(JsonElement block) => GetBlockString(block, "blockId");

    public static int GetPageIdx(JsonElement block) => GetBlockInt(block, "pageIdx");

    /// <summary>读取块 pageBBoxes：[{pageIdx, bbox}, ...]，跨页段落/表格每页一段。</summary>
    private static List<(int PageIdx, double[] Bbox)> ReadPageBBoxes(JsonElement block)
    {
        var result = new List<(int, double[])>();
        if (!block.TryGetProperty("pageBBoxes", out var pageBBoxes) || pageBBoxes.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var fallbackPageIdx = GetBlockInt(block, "pageIdx");
        foreach (var entry in pageBBoxes.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var pageIdx = entry.TryGetProperty("pageIdx", out var pageIdxEl) && pageIdxEl.ValueKind == JsonValueKind.Number
                ? pageIdxEl.GetInt32()
                : fallbackPageIdx;
            var bbox = ReadBbox(entry);
            if (bbox.Length == 4)
            {
                result.Add((pageIdx, bbox));
            }
        }

        return result;
    }

    /// <summary>读取块 mergedBBoxes（并排/合并块多个矩形），沿用块所在页。</summary>
    private static List<double[]> ReadMergedBBoxes(JsonElement block)
    {
        var result = new List<double[]>();
        if (!block.TryGetProperty("mergedBBoxes", out var merged) || merged.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in merged.EnumerateArray())
        {
            var bbox = ReadRawBbox(item);
            if (bbox.Length == 4)
            {
                result.Add(bbox);
            }
        }

        return result;
    }

    /// <summary>读取裸 bbox 数组 [x0,y0,x1,y1]（mergedBBoxes 的每个元素），也兼容 {bbox:[...]} 对象。</summary>
    private static double[] ReadRawBbox(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("bbox", out var bboxProp))
        {
            return ReadBbox(value);
        }

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 4)
        {
            return Array.Empty<double>();
        }

        var values = new double[4];
        var i = 0;
        foreach (var item in value.EnumerateArray())
        {
            values[i++] = item.ValueKind == JsonValueKind.Number ? item.GetDouble() : 0;
        }

        return values;
    }

    private static double[] ReadBbox(JsonElement block)
    {
        if (!block.TryGetProperty("bbox", out var bbox)
            || bbox.ValueKind != JsonValueKind.Array
            || bbox.GetArrayLength() != 4)
        {
            return Array.Empty<double>();
        }

        var values = new double[4];
        var i = 0;
        foreach (var item in bbox.EnumerateArray())
        {
            values[i++] = item.ValueKind == JsonValueKind.Number ? item.GetDouble() : 0;
        }

        return values;
    }

    private static string? GetBlockString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetBlockInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}

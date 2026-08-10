using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Documents;

/// <summary>
/// 内部适配 IR 校验（v2 文档 §2/§4/§5 硬性要求，取代 spec §4.3）：
/// 1) bbox 必须为 0~1 归一化坐标（拒绝像素坐标/负值/倒置）；
/// 2) source/confidence 允许缺省（v2 §4 降级期）；存在时 source∈native|ocr、confidence∈[0,1]、native 恒 1.0；
/// 3) blockId 文档内唯一（= AnGIneer block_uid）；
/// 4) table 块必须同时给 html 与整表截图 imgPath；
/// 另校验 schemaVersion/docId/meta/pages 必填与页面真实尺寸。
/// </summary>
public class IrValidator : IIrValidator, ITransientDependency
{
    // seal 为保留类型（spec §4.3.5）：AnGIneer 当前不产出（v2 §3 映射表无此项）
    private static readonly HashSet<string> BlockTypes = new()
    {
        "title", "para", "table", "list", "image", "equation", "seal", "header", "footer"
    };

    public IrValidationResult Validate(string irJson)
    {
        var errors = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(irJson);
        }
        catch (JsonException ex)
        {
            return new IrValidationResult(new[] { $"内部适配 IR 不是合法 JSON：{ex.Message}" });
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new IrValidationResult(new[] { "内部适配 IR 根节点必须是对象" });
            }

            if (!TryGetNonEmptyString(root, "schemaVersion", out _))
            {
                errors.Add("缺少必填字段 schemaVersion");
            }
            if (!TryGetNonEmptyString(root, "docId", out _))
            {
                errors.Add("缺少必填字段 docId");
            }

            if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
            {
                errors.Add("缺少必填字段 pages（数组）");
            }
            else
            {
                foreach (var page in pages.EnumerateArray())
                {
                    if (!page.TryGetProperty("pageIdx", out var idx) || idx.ValueKind != JsonValueKind.Number)
                    {
                        errors.Add("pages[] 缺少 pageIdx");
                        continue;
                    }
                    var width = GetDouble(page, "width");
                    var height = GetDouble(page, "height");
                    if (width <= 0 || height <= 0)
                    {
                        errors.Add($"pages[{idx.GetInt32()}] width/height 必须为正数（页面真实尺寸，v2 §1 meta pages）");
                    }
                }
            }

            if (!root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
            {
                errors.Add("缺少必填字段 meta");
            }
            else if (!TryGetNonEmptyString(meta, "fileName", out _))
            {
                errors.Add("缺少必填字段 meta.fileName");
            }

            if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            {
                errors.Add("缺少必填字段 blocks（数组）");
            }
            else
            {
                var seenBlockIds = new HashSet<string>();
                foreach (var block in blocks.EnumerateArray())
                {
                    ValidateBlock(block, seenBlockIds, errors);
                }
            }
        }

        return new IrValidationResult(errors);
    }

    /// <summary>spec §4.5：source=ocr 且 confidence&lt;0.5 的块占比。v2 §4：source/confidence 缺失的块不参与统计；全部缺失时返回 0（提示降级关闭）。</summary>
    public static double CalculateOcrLowConfidenceRatio(JsonElement root)
    {
        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var total = 0;
        var lowConfidence = 0;
        foreach (var block in blocks.EnumerateArray())
        {
            var source = block.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() : null;
            var confidence = GetDouble(block, "confidence");
            if (source == null || confidence < 0)
            {
                continue; // v2 §4：缺省块不参与（GetDouble 缺省约定返回 -1）
            }
            total++;
            if (source == "ocr" && confidence < 0.5)
            {
                lowConfidence++;
            }
        }
        return total == 0 ? 0 : (double)lowConfidence / total;
    }

    private static void ValidateBlock(
        JsonElement block,
        HashSet<string> seenBlockIds,
        List<string> errors)
    {
        var label = "block";
        if (TryGetNonEmptyString(block, "blockId", out var blockId))
        {
            label = $"block[{blockId}]";
            if (!seenBlockIds.Add(blockId))
            {
                errors.Add($"{label} blockId 重复（须文档内唯一）");
            }
        }
        else
        {
            errors.Add("block 缺少必填字段 blockId");
        }

        if (!block.TryGetProperty("pageIdx", out var pageIdxEl) || pageIdxEl.ValueKind != JsonValueKind.Number)
        {
            errors.Add($"{label} 缺少 pageIdx");
        }

        if (!block.TryGetProperty("bbox", out var bbox) || bbox.ValueKind != JsonValueKind.Array ||
            bbox.GetArrayLength() != 4)
        {
            errors.Add($"{label} bbox 必须为 [x0,y0,x1,y1] 四元数组");
        }
        else
        {
            var values = bbox.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : -1).ToArray();
            if (values.Any(v => v < 0) || values[2] <= values[0] || values[3] <= values[1])
            {
                errors.Add($"{label} bbox 坐标非法（须 x1>x0 且 y1>y0，非负）");
            }
            else if (values.Any(v => v > 1.0))
            {
                // v2 §2：bbox 为 0~1 归一化坐标，超出区间即疑似像素坐标，拒收
                errors.Add($"{label} bbox 超出 0~1 归一化区间（疑似像素坐标，v2 不要求像素坐标）");
            }
        }

        if (!TryGetNonEmptyString(block, "type", out var type) || !BlockTypes.Contains(type))
        {
            errors.Add($"{label} type 非法（须为 {string.Join("|", BlockTypes)}）");
        }

        // v2 §4：source/confidence 允许缺省（AnGIneer 补齐前）；存在时才校验取值
        if (block.TryGetProperty("source", out var sourceEl) && sourceEl.ValueKind != JsonValueKind.Null)
        {
            if (!TryGetNonEmptyString(block, "source", out var source) || (source != "native" && source != "ocr"))
            {
                errors.Add($"{label} source 必须为 native|ocr（v2 §4）");
            }
            else if (source == "native" &&
                     block.TryGetProperty("confidence", out var confEl) && confEl.ValueKind == JsonValueKind.Number &&
                     confEl.GetDouble() != 1.0)
            {
                errors.Add($"{label} source=native 时 confidence 必须为 1.0（v2 §4）");
            }
        }

        var confidence = GetDouble(block, "confidence");
        if (confidence != -1 && (confidence < 0 || confidence > 1)) // GetDouble 缺省约定返回 -1
        {
            errors.Add($"{label} confidence 必须在 0~1（v2 §4）");
        }

        if (type == "table")
        {
            if (!block.TryGetProperty("table", out var table) || table.ValueKind != JsonValueKind.Object ||
                !TryGetNonEmptyString(table, "html", out _))
            {
                errors.Add($"{label} table 块缺少 table.html（spec §4.3-4）");
            }
            if (table.ValueKind != JsonValueKind.Object || !TryGetNonEmptyString(table, "imgPath", out _))
            {
                errors.Add($"{label} table 块缺少 table.imgPath 整表截图（spec §4.3-4）");
            }
        }

        if (type is "image" or "seal" or "equation" && !TryGetNonEmptyString(block, "imgPath", out _))
        {
            errors.Add($"{label} {type} 块缺少 imgPath");
        }
    }

    private static bool TryGetNonEmptyString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
        return false;
    }

    private static double GetDouble(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetDouble()
            : -1;
    }
}

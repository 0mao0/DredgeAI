using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.TenderReadings.Extractors;

/// <summary>P1 项目信息抽取：目录首节点/文件名 → 项目名称；块文本正则 → 项目编号。</summary>
public class ProjectInfoExtractor : IBaselineFieldExtractor, ITransientDependency
{
    private static readonly Regex ProjectCodePattern = new(
        @"(?:项目编号|招标编号|采购编号|项目代码)\s*[:：]?\s*([A-Za-z0-9][A-Za-z0-9\-_/]{2,63})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ProjectNamePattern = new(
        @"项目名称\s*[:：]?\s*([^\r\n]{4,120})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public BaselineCategory Category => BaselineCategory.ProjectInfo;

    public Task<IReadOnlyList<BaselineFieldDraft>> ExtractAsync(
        BaselineExtractionContext context,
        CancellationToken cancellationToken = default)
    {
        var root = context.IrRoot;
        var drafts = new List<BaselineFieldDraft>();

        var name = ReadProjectName(root);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var nameRef = FindFirstBlock(root, text => text.Contains(name, StringComparison.OrdinalIgnoreCase));
            drafts.Add(new BaselineFieldDraft
            {
                FieldKey = "name",
                ValueJson = JsonSerializer.Serialize(new { value = name }),
                RawText = name,
                Confidence = 0.95,
                Status = BaselineFieldStatus.Auto,
                Extractor = "rule",
                ExtractorVersion = "1.0",
                SourceRefs = ToDraftList(nameRef)
            });
        }

        foreach (var block in EnumerateBlocks(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = GetString(block, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var match = ProjectCodePattern.Match(text);
            if (match.Success)
            {
                drafts.Add(new BaselineFieldDraft
                {
                    FieldKey = "code",
                    ValueJson = JsonSerializer.Serialize(new { value = match.Groups[1].Value.Trim() }),
                    RawText = match.Value,
                    Confidence = 0.9,
                    Status = BaselineFieldStatus.Auto,
                    Extractor = "rule",
                    ExtractorVersion = "1.0",
                    SourceRefs = ToDraftList(block)
                });
                break;
            }
        }

        return Task.FromResult<IReadOnlyList<BaselineFieldDraft>>(drafts);
    }

    private static string? ReadProjectName(JsonElement root)
    {
        // 优先从正文中提取“项目名称：xxx”，避免目录首节点是“1.6 保密”这类非项目名
        foreach (var block in EnumerateBlocks(root))
        {
            var text = GetString(block, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var match = ProjectNamePattern.Match(text);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        // 其次取目录里包含“工程/项目”的标题
        if (root.TryGetProperty("outline", out var outline)
            && outline.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in outline.EnumerateArray())
            {
                if (node.TryGetProperty("title", out var title)
                    && title.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(title.GetString())
                    && (title.GetString()!.Contains("工程", StringComparison.Ordinal)
                        || title.GetString()!.Contains("项目", StringComparison.Ordinal)))
                {
                    return title.GetString()!.Trim();
                }
            }
        }

        if (root.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("fileName", out var fileName)
            && fileName.ValueKind == JsonValueKind.String)
        {
            return Path.GetFileNameWithoutExtension(fileName.GetString());
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateBlocks(JsonElement root)
    {
        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var block in blocks.EnumerateArray())
        {
            yield return block;
        }
    }

    private static JsonElement? FindFirstBlock(JsonElement root, Func<string, bool> predicate)
    {
        foreach (var block in EnumerateBlocks(root))
        {
            var text = GetString(block, "text");
            if (!string.IsNullOrWhiteSpace(text) && predicate(text))
            {
                return block;
            }
        }

        return null;
    }

    private static List<SourceMapItemDraft> ToDraftList(JsonElement? block)
    {
        var list = new List<SourceMapItemDraft>();
        if (block == null)
        {
            return list;
        }

        list.Add(new SourceMapItemDraft
        {
            BlockId = GetString(block.Value, "blockId") ?? string.Empty,
            PageIdx = GetInt(block.Value, "pageIdx"),
            Bbox = GetBbox(block.Value),
            Text = GetString(block.Value, "text") ?? string.Empty
        });
        return list;
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static double[] GetBbox(JsonElement element)
    {
        if (!element.TryGetProperty("bbox", out var bbox)
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
}

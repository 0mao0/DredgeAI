using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.AnGineer;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>
/// AI 晨会施工项目：项目下拉数据源 + 施工方案解析后的项目信息/主要内容提取。
/// 施工方案本体走既有知识库上传链路（AnGIneer 解析入知识库），本服务只维护项目档案。
/// </summary>
[RemoteService(false)] // 精确路由由 HttpApi 显式 Controller 暴露（/api/meeting/projects）
public class MeetingProjectAppService : ApplicationService, IMeetingProjectAppService
{
    private readonly IRepository<MeetingProject, Guid> _projects;
    private readonly IAnGineerClient _anGineer;
    private readonly ILlmGateway _llmGateway;

    public MeetingProjectAppService(
        IRepository<MeetingProject, Guid> projects,
        IAnGineerClient anGineer,
        ILlmGateway llmGateway)
    {
        _projects = projects;
        _anGineer = anGineer;
        _llmGateway = llmGateway;
    }

    public async Task<List<MeetingProjectDto>> GetListAsync()
    {
        var all = await _projects.GetListAsync();
        return all.OrderByDescending(p => p.CreationTime).Select(Map).ToList();
    }

    public async Task<MeetingProjectDto> CreateAsync(CreateMeetingProjectInput input)
    {
        var project = new MeetingProject(GuidGenerator.Create(), input.Name.Trim());
        var docIds = input.DocIds.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (docIds.Count > 0)
        {
            project.AttachPlans(
                docIds,
                await SafeStateAsync(docIds[0]),
                AlignNames(input.DocNames, docIds));
        }
        else if (!string.IsNullOrWhiteSpace(input.DocId))
        {
            var docId = input.DocId.Trim();
            project.AttachPlan(
                docId,
                await SafeStateAsync(docId),
                input.DocNames?.FirstOrDefault());
        }
        await _projects.InsertAsync(project);
        return Map(project);
    }

    public async Task<MeetingProjectDto> UpdateAsync(Guid id, UpdateMeetingProjectInput input)
    {
        var project = await _projects.GetAsync(id);
        project.SetName(input.Name.Trim());

        var docIds = input.DocIds.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (docIds.Count > 0)
        {
            project.AttachPlans(
                docIds,
                await SafeStateAsync(docIds[0]),
                AlignNames(input.DocNames, docIds));
        }
        else
        {
            project.ClearPlans();
        }
        await _projects.UpdateAsync(project);
        return Map(project);
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _projects.GetAsync(id);
        foreach (var docId in ParseDocIds(project.DocIdsJson))
        {
            try
            {
                await _anGineer.DeleteDocumentAsync(docId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "删除施工方案文档失败（{DocId}），继续删除项目", docId);
            }
        }
        await _projects.DeleteAsync(project);
    }

    public async Task<MeetingProjectDto> GetAsync(Guid id)
    {
        var project = await _projects.GetAsync(id);
        // 同步刷新解析状态（供前端轮询）
        if (!string.IsNullOrWhiteSpace(project.AnGineerDocId) && project.Status is "processing" or "ready")
        {
            var state = await SafeStateAsync(project.AnGineerDocId);
            if (state == "failed" && project.Status != "failed")
            {
                project.MarkFailed();
                await _projects.UpdateAsync(project);
            }
            else if (state == "processing" && project.Status != "processing")
            {
                project.AttachPlan(project.AnGineerDocId, "processing");
                await _projects.UpdateAsync(project);
            }
        }
        return Map(project);
    }

    public async Task<MeetingProjectDto> ExtractAsync(Guid id)
    {
        var project = await _projects.GetAsync(id);
        if (string.IsNullOrWhiteSpace(project.AnGineerDocId))
        {
            throw new BusinessException("MEETING_PROJECT_NO_PLAN", "请先上传施工方案");
        }
        var state = await _anGineer.GetStateAsync(project.AnGineerDocId);
        if (state.State != AnGineerJobState.Succeeded)
        {
            throw new BusinessException(
                "MEETING_PROJECT_PARSE_NOT_READY",
                $"施工方案尚未解析完成（{state.Stage ?? state.FailureReason ?? "处理中"}）");
        }

        var text = await ReadContentMdAsync(project.AnGineerDocId);
        var (projectInfoJson, summary) = await ExtractProjectInfoAsync(project.Name, text);
        project.SetExtraction(projectInfoJson, summary);
        await _projects.UpdateAsync(project);
        return Map(project);
    }

    public async Task<ProjectNameSuggestionDto> SuggestNameAsync(string docId)
    {
        if (string.IsNullOrWhiteSpace(docId))
        {
            return new ProjectNameSuggestionDto();
        }
        var state = await _anGineer.GetStateAsync(docId);
        if (state.State != AnGineerJobState.Succeeded)
        {
            throw new BusinessException(
                "MEETING_PROJECT_PARSE_NOT_READY",
                $"施工方案尚未解析完成（{state.Stage ?? state.FailureReason ?? "处理中"}）");
        }
        var text = await ReadContentMdAsync(docId);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ProjectNameSuggestionDto();
        }
        try
        {
            var raw = await _llmGateway.CompleteAsync(
                "你是施工项目管理助手。只输出指定 JSON，不要输出其他内容。",
                "请根据下面的施工方案文本，提取其中的项目名称，仅返回如下 JSON：\n" +
                "{\"projectName\":\"项目名称\"}\n\n施工方案文本：\n" + Truncate(text, 12000));
            var (name, _, _) = ParseProjectJson(raw);
            return new ProjectNameSuggestionDto { Name = name.Trim() };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "提取项目名称失败（{DocId}），返回空", docId);
            return new ProjectNameSuggestionDto();
        }
    }

    public async Task<MeetingProjectDocumentFileResult> GetDocumentFileAsync(Guid projectId, string docId)
    {
        var project = await _projects.GetAsync(projectId);
        EnsureDocBelongs(project, docId);
        var content = await _anGineer.OpenPdfAsync(docId);
        return new MeetingProjectDocumentFileResult
        {
            Content = content,
            ContentType = "application/pdf"
        };
    }

    public async Task<MeetingProjectDocumentContentDto> GetDocumentContentAsync(Guid projectId, string docId)
    {
        var project = await _projects.GetAsync(projectId);
        EnsureDocBelongs(project, docId);
        var markdown = await _anGineer.GetContentAsync(docId);
        return new MeetingProjectDocumentContentDto { Markdown = markdown };
    }

    private static void EnsureDocBelongs(MeetingProject project, string docId)
    {
        if (!ParseDocIds(project.DocIdsJson).Contains(docId, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException("MEETING_PROJECT_DOC_NOT_FOUND", "施工方案不属于当前项目");
        }
    }

    private async Task<string> SafeStateAsync(string docId)
    {
        try
        {
            return await _anGineer.GetStateAsync(docId) switch
            {
                { State: AnGineerJobState.Succeeded } => "ready",
                { State: AnGineerJobState.Failed or AnGineerJobState.Partial } => "failed",
                _ => "processing"
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "查询施工方案解析状态失败（{DocId}），按处理中处理", docId);
            return "processing";
        }
    }

    private static IReadOnlyList<string> AlignNames(List<string>? names, List<string> docIds)
    {
        return docIds.Select((_, i) => names is { Count: > 0 } && i < names.Count ? names[i] ?? "" : "").ToList();
    }

    private async Task<string> ReadContentMdAsync(string docId)
    {
        try
        {
            var artifacts = await _anGineer.ListArtifactsAsync(docId);
            var contentArtifact = artifacts.FirstOrDefault(a =>
                a.Name.Contains("content", StringComparison.OrdinalIgnoreCase)
                || a.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
            if (contentArtifact is null)
            {
                return "";
            }
            await using var stream = await _anGineer.OpenArtifactAsync(docId, contentArtifact);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "读取施工方案解析产物失败（{DocId}），按空文本处理", docId);
            return "";
        }
    }

    private async Task<(string ProjectInfoJson, string Summary)> ExtractProjectInfoAsync(string projectName, string text)
    {
        const string systemPrompt = "你是施工项目管理助手。只输出指定 JSON，不要输出其他内容。";
        var userPrompt =
            "请根据下面的施工方案文本，提取项目信息与主要内容，仅返回如下 JSON：\n" +
            "{\"projectName\":\"项目名称\",\"projectInfo\":\"项目概况（地点/工期/规模等，一句话）\"," +
            "\"mainContent\":\"施工范围与主要工序、安全重点（200字以内）\"}\n\n" +
            $"已知项目名称：{projectName}\n\n施工方案文本：\n{Truncate(text, 12000)}";
        try
        {
            var raw = await _llmGateway.CompleteAsync(systemPrompt, userPrompt);
            var (name, info, mainContent) = ParseProjectJson(raw);
            var infoJson = JsonSerializer.Serialize(new
            {
                projectName = string.IsNullOrWhiteSpace(name) ? projectName : name,
                projectInfo = info
            });
            return (infoJson, mainContent);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "提取项目信息失败，使用空信息兜底");
            var infoJson = JsonSerializer.Serialize(new { projectName, projectInfo = "" });
            return (infoJson, "");
        }
    }

    private static (string Name, string Info, string MainContent) ParseProjectJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return ("", "", "");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw.Substring(start, end - start + 1));
            var root = doc.RootElement;
            return (
                ReadString(root, "projectName", "name"),
                ReadString(root, "projectInfo", "info", "项目概况"),
                ReadString(root, "mainContent", "summary", "主要内容"));
        }
        catch (JsonException)
        {
            return ("", "", "");
        }
    }

    private static string ReadString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString()?.Trim() ?? "";
            }
        }
        return "";
    }

    private static string Truncate(string text, int maxChars)
    {
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    private static MeetingProjectDto Map(MeetingProject project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        AnGineerDocId = project.AnGineerDocId,
        DocIds = ParseDocIds(project.DocIdsJson),
        DocNames = ParseDocNames(project.DocNamesJson, ParseDocIds(project.DocIdsJson).Count),
        Status = project.Status,
        ProjectInfoJson = project.ProjectInfoJson,
        Summary = project.Summary,
        CreationTime = project.CreationTime
    };

    private static List<string> ParseDocIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static List<string> ParseDocNames(string json, int count)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return Enumerable.Repeat("", count).ToList();
        }
        try
        {
            var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
            while (names.Count < count)
            {
                names.Add("");
            }
            return names.Take(count).ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return Enumerable.Repeat("", count).ToList();
        }
    }
}

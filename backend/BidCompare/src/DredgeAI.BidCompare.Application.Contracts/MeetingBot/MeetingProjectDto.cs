using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.MeetingBot;

public class MeetingProjectDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? AnGineerDocId { get; set; }

    public List<string> DocIds { get; set; } = new();

    /// <summary>与 DocIds 对齐的原始文件名列表。</summary>
    public List<string> DocNames { get; set; } = new();

    public string Status { get; set; } = "ready";

    public string ProjectInfoJson { get; set; } = "{}";

    public string Summary { get; set; } = "";

    public DateTime CreationTime { get; set; }
}

public class CreateMeetingProjectInput
{
    public string Name { get; set; } = "";

    /// <summary>施工方案经 AnGIneer 解析后的 doc id（已入知识库）。</summary>
    public string DocId { get; set; } = "";

    /// <summary>施工方案经 AnGIneer 解析后的多个 doc id（支持多份方案）。</summary>
    public List<string> DocIds { get; set; } = new();

    /// <summary>与 DocIds 对齐的原始文件名列表。</summary>
    public List<string>? DocNames { get; set; }
}

public class UpdateMeetingProjectInput
{
    public string Name { get; set; } = "";

    /// <summary>编辑后的施工方案 doc id 全量列表（删除/新增后提交）。</summary>
    public List<string> DocIds { get; set; } = new();

    /// <summary>与 DocIds 对齐的原始文件名列表。</summary>
    public List<string>? DocNames { get; set; }
}

public class SuggestProjectNameInput
{
    public string DocId { get; set; } = "";
}

public class ProjectNameSuggestionDto
{
    public string Name { get; set; } = "";
}

public class MeetingProjectDocumentFileResult
{
    public Stream Content { get; set; } = Stream.Null;

    public string ContentType { get; set; } = "application/pdf";
}

public class MeetingProjectDocumentContentDto
{
    public string Markdown { get; set; } = "";
}

public interface IMeetingProjectAppService
{
    Task<List<MeetingProjectDto>> GetListAsync();

    Task<MeetingProjectDto> CreateAsync(CreateMeetingProjectInput input);

    Task<MeetingProjectDto> UpdateAsync(Guid id, UpdateMeetingProjectInput input);

    Task DeleteAsync(Guid id);

    Task<MeetingProjectDto> GetAsync(Guid id);

    /// <summary>读取施工方案解析产物，用 LLM 提取项目信息与主要内容。</summary>
    Task<MeetingProjectDto> ExtractAsync(Guid id);

    /// <summary>从已解析的施工方案中提取项目名称（用于新建项目时自动填入）。</summary>
    Task<ProjectNameSuggestionDto> SuggestNameAsync(string docId);

    /// <summary>读取项目内某份施工方案的 PDF 原文（docs-ui PDF_Viewer 预览用）。</summary>
    Task<MeetingProjectDocumentFileResult> GetDocumentFileAsync(Guid projectId, string docId);

    /// <summary>读取项目内某份施工方案解析后的 Markdown 正文。</summary>
    Task<MeetingProjectDocumentContentDto> GetDocumentContentAsync(Guid projectId, string docId);
}

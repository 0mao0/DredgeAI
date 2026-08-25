using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>AI 晨会施工项目：选择项目后上传施工方案（AnGIneer 解析入知识库），并提取项目信息供晨会稿生成引用。</summary>
public class MeetingProject : FullAuditedEntity<Guid>
{
    public string Name { get; private set; } = "";

    /// <summary>AnGIneer 解析产物 doc id（知识库检索用）。</summary>
    public string? AnGineerDocId { get; private set; }

    /// <summary>全部施工方案解析产物 doc id 列表（JSON 数组），用于知识库检索与后续提取。</summary>
    public string DocIdsJson { get; private set; } = "[]";

    /// <summary>与 DocIdsJson 对齐的原始文件名列表（JSON 数组），用于编辑抽屉展示。</summary>
    public string DocNamesJson { get; private set; } = "[]";

    /// <summary>解析状态：processing / ready / failed。</summary>
    public string Status { get; private set; } = "ready";

    /// <summary>LLM 提取的项目信息（JSON）。</summary>
    public string ProjectInfoJson { get; private set; } = "{}";

    /// <summary>LLM 提取的施工方案主要内容。</summary>
    public string Summary { get; private set; } = "";

    protected MeetingProject()
    {
    }

    public MeetingProject(Guid id, string name) : base(id)
    {
        Name = name;
    }

    public void AttachPlan(string docId, string status = "ready", string? docName = null)
    {
        AnGineerDocId = docId;
        DocIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { docId });
        DocNamesJson = System.Text.Json.JsonSerializer.Serialize(
            string.IsNullOrWhiteSpace(docName) ? new[] { "" } : new[] { docName });
        Status = status;
    }

    public void AttachPlans(
        IReadOnlyList<string> docIds,
        string status = "ready",
        IReadOnlyList<string>? docNames = null)
    {
        var ids = docIds.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        AnGineerDocId = ids.FirstOrDefault();
        DocIdsJson = System.Text.Json.JsonSerializer.Serialize(ids);
        var names = docNames ?? [];
        DocNamesJson = System.Text.Json.JsonSerializer.Serialize(
            ids.Select((_, i) => i < names.Count ? names[i] ?? "" : ""));
        Status = status;
    }

    public void SetName(string name)
    {
        Name = name;
    }

    public void ClearPlans()
    {
        AnGineerDocId = null;
        DocIdsJson = "[]";
        DocNamesJson = "[]";
        Status = "ready";
    }

    public void SetExtraction(string projectInfoJson, string summary)
    {
        ProjectInfoJson = projectInfoJson;
        Summary = summary;
        Status = "ready";
    }

    public void MarkFailed()
    {
        Status = "failed";
    }
}

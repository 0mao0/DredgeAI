using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.MeetingBot;

public class PreInfoInput
{
    public DateTime Date { get; set; }

    public string Weather { get; set; } = "";

    public string Tasks { get; set; } = "";

    public string RiskPoints { get; set; } = "";

    /// <summary>所选施工项目名称（晨会稿生成引用项目上下文）。</summary>
    public string ProjectName { get; set; } = "";

    /// <summary>所选项目施工方案提取的主要内容（晨会稿生成引用）。</summary>
    public string ProjectSummary { get; set; } = "";
}

public class UpdateSpeechInput
{
    public string Content { get; set; } = "";
}

public class AskQaInput
{
    public string Question { get; set; } = "";
}

public class PlanParseInput
{
    public string PlanText { get; set; } = "";
}

public class PlanParseResult
{
    public DateTime Date { get; set; }

    public string Weather { get; set; } = "";

    public string Tasks { get; set; } = "";

    public string RiskPoints { get; set; } = "";

    public string City { get; set; } = "";
}

public class SpeechAudioStatusDto
{
    public bool Cached { get; set; }

    /// <summary>开场句是否已预合成缓存（点播放时第一句秒出）。</summary>
    public bool LeadCached { get; set; }

    /// <summary>已缓存的开幕句文本，前端需与当前稿首段一致才复用。</summary>
    public string LeadText { get; set; } = "";
}

public class MeetingRecordDto
{
    public Guid Id { get; set; }

    public DateTime Date { get; set; }

    public string PreInfoJson { get; set; } = "{}";

    public MeetingStatus Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public SpeechDraftDto? SpeechDraft { get; set; }

    public List<AttendanceItemDto> Attendance { get; set; } = new();

    public List<QaRecordDto> QaRecords { get; set; } = new();

    public ReportDto? Report { get; set; }

    public DateTime CreationTime { get; set; }
}

public class MeetingHistoryDto
{
    public Guid Id { get; set; }

    public DateTime Date { get; set; }

    public string TaskPreview { get; set; } = "";

    public MeetingStatus Status { get; set; }

    public DateTime CreationTime { get; set; }
}

public class SpeechDraftDto
{
    public Guid Id { get; set; }

    public string Content { get; set; } = "";

    public string Status { get; set; } = "draft";

    public DateTime UpdatedAt { get; set; }
}

public class AttendanceItemDto
{
    public Guid? WorkerId { get; set; }

    public string Name { get; set; } = "";

    public string Team { get; set; } = "";

    public AttendanceStatus Status { get; set; }

    public double Confidence { get; set; }

    public double[] Bbox { get; set; } = [];

    /// <summary>工人证件号（身份证号），前端可据此展示“姓名-生日后四位”以区分同名人员。</summary>
    public string EmployeeNo { get; set; } = "";

    /// <summary>工人人脸照片访问地址（可能为空）。</summary>
    public string? FacePhotoUrl { get; set; }
}

public class QaRecordDto
{
    public Guid Id { get; set; }

    public string Question { get; set; } = "";

    public string Answer { get; set; } = "";

    public QaIntentType IntentType { get; set; }

    public List<string> Sources { get; set; } = new();

    public DateTime CreatedAt { get; set; }
}

public class ReportDto
{
    public Guid Id { get; set; }

    public string Transcript { get; set; } = "";

    public List<AttendanceItemDto> Attendance { get; set; } = new();

    public List<QaRecordDto> QaRecords { get; set; } = new();

    public List<UnrecognizedFaceDto> UnrecognizedFaces { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public string? ReportUrl { get; set; }
}

public class UnrecognizedFaceDto
{
    public Guid Id { get; set; }

    public string PhotoUrl { get; set; } = "";

    public double Confidence { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class WorkerDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string EmployeeNo { get; set; } = "";

    public string Team { get; set; } = "";

    public FaceStatus FaceStatus { get; set; }
}

public class WorkerCreateInput
{
    public string Name { get; set; } = "";

    public string EmployeeNo { get; set; } = "";

    public string Team { get; set; } = "";
}

public class IdCardRecognitionDto
{
    public string Name { get; set; } = "";

    public string IdCardNumber { get; set; } = "";

    public string Gender { get; set; } = "";

    public string Nation { get; set; } = "";

    public string BirthDate { get; set; } = "";

    public string Address { get; set; } = "";

    public string RawText { get; set; } = "";
}

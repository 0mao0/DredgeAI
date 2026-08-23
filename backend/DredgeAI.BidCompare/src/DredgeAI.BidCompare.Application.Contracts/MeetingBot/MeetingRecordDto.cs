using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.MeetingBot;

public class PreInfoInput
{
    public DateTime Date { get; set; }

    public string Weather { get; set; } = "";

    public string Tasks { get; set; } = "";

    public string RiskPoints { get; set; } = "";
}

public class UpdateSpeechInput
{
    public string Content { get; set; } = "";
}

public class AskQaInput
{
    public string Question { get; set; } = "";
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

    public DateTime CreatedAt { get; set; }

    public string? ReportUrl { get; set; }
}

public class WorkerDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string EmployeeNo { get; set; } = "";

    public string Team { get; set; } = "";

    public FaceStatus FaceStatus { get; set; }
}

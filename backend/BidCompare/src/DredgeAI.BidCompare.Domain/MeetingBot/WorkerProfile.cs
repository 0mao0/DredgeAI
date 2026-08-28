using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>工人档案（花名册 + 人脸库）。</summary>
public class WorkerProfile : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;

    public string EmployeeNo { get; private set; } = default!;

    public string Team { get; private set; } = "";

    public FaceStatus FaceStatus { get; private set; }

    /// <summary>人脸照片存储 key（JSON 数组）。</summary>
    public string FacePhotosJson { get; private set; } = "[]";

    public DateTime? FaceEnrolledAt { get; private set; }

    protected WorkerProfile()
    {
    }

    public WorkerProfile(Guid id, string name, string employeeNo, string team) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 64);
        EmployeeNo = Check.NotNullOrWhiteSpace(employeeNo, nameof(employeeNo), maxLength: 32);
        Team = team ?? "";
        FaceStatus = FaceStatus.Pending;
    }

    public void MarkEnrolled(string facePhotosJson)
    {
        FacePhotosJson = string.IsNullOrWhiteSpace(facePhotosJson) ? "[]" : facePhotosJson;
        FaceStatus = FaceStatus.Enrolled;
        FaceEnrolledAt = DateTime.Now;
    }
}

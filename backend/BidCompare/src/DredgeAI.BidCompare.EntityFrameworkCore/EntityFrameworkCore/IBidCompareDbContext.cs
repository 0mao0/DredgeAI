using Microsoft.EntityFrameworkCore;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.TenderReadings;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

[ConnectionStringName(BidCompareDbProperties.ConnectionStringName)]
public interface IBidCompareDbContext : IEfCoreDbContext
{
    DbSet<CompareTask> CompareTasks { get; set; }
    DbSet<CompareDocument> CompareDocuments { get; set; }
    DbSet<CompareDraftDocument> CompareDraftDocuments { get; set; }
    DbSet<EvidenceItem> EvidenceItems { get; set; }
    DbSet<ClauseTemplate> ClauseTemplates { get; set; }
    DbSet<ExportJob> ExportJobs { get; set; }
    DbSet<AiUsageRecord> AiUsageRecords { get; set; }
    DbSet<TenderReadingTask> TenderReadingTasks { get; set; }
    DbSet<TenderReadingDocument> TenderReadingDocuments { get; set; }
    DbSet<BaselineField> BaselineFields { get; set; }
    DbSet<SourceMapItem> SourceMapItems { get; set; }
    DbSet<MeetingRecord> MeetingRecords { get; set; }
    DbSet<SpeechDraft> SpeechDrafts { get; set; }
    DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    DbSet<MeetingProject> MeetingProjects { get; set; }
    DbSet<UnrecognizedFace> UnrecognizedFaces { get; set; }
    DbSet<QaRecord> QaRecords { get; set; }
    DbSet<WorkerProfile> WorkerProfiles { get; set; }
}

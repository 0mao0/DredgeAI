using Microsoft.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.TenderReadings;
using Shiw.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

[ConnectionStringName(BidCompareDbProperties.ConnectionStringName)]
public class BidCompareDbContext :
    AbpDbContext<BidCompareDbContext>
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    public DbSet<CompareTask> CompareTasks { get; set; }
    public DbSet<CompareDocument> CompareDocuments { get; set; }
    public DbSet<CompareDraftDocument> CompareDraftDocuments { get; set; }
    public DbSet<EvidenceItem> EvidenceItems { get; set; }
    public DbSet<ClauseTemplate> ClauseTemplates { get; set; }
    public DbSet<ExportJob> ExportJobs { get; set; }
    public DbSet<AiUsageRecord> AiUsageRecords { get; set; }
    public DbSet<TenderReadingTask> TenderReadingTasks { get; set; }
    public DbSet<TenderReadingDocument> TenderReadingDocuments { get; set; }
    public DbSet<BaselineField> BaselineFields { get; set; }
    public DbSet<SourceMapItem> SourceMapItems { get; set; }
    public DbSet<MeetingRecord> MeetingRecords { get; set; }
    public DbSet<SpeechDraft> SpeechDrafts { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    public DbSet<MeetingProject> MeetingProjects { get; set; }

    public DbSet<UnrecognizedFace> UnrecognizedFaces { get; set; }
    public DbSet<QaRecord> QaRecords { get; set; }
    public DbSet<WorkerProfile> WorkerProfiles { get; set; }

    private readonly IShiwDbContextHandler _handler;

    public BidCompareDbContext(
        DbContextOptions<BidCompareDbContext> options,
        IShiwDbContextHandler handler)
        : base(options)
    {
        _handler = handler;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigureShiwBackgroundJobs(_handler);
        /* Configure your own tables/entities inside here */

        builder.ConfigureBidCompare(_handler);
    }
}

using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.TenderReadings;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class BidCompareDbContext :
    AbpDbContext<BidCompareDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    #region Entities from the modules

    /* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }
    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

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
    public DbSet<QaRecord> QaRecords { get; set; }
    public DbSet<WorkerProfile> WorkerProfiles { get; set; }

    public BidCompareDbContext(DbContextOptions<BidCompareDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();

        /* Configure your own tables/entities inside here */

        builder.Entity<CompareTask>(b =>
        {
            b.ToTable("BcCompareTasks");
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.ClauseSnapshotJson).HasColumnType("text");
            b.Property(x => x.ClauseDraftsJson).HasColumnType("text");
            b.Property(x => x.ReportJson).HasColumnType("text");
            b.Property(x => x.ProgressStage).IsRequired().HasMaxLength(32);
            b.Property(x => x.ProgressMessage).HasMaxLength(1024);
            b.Property(x => x.FailureReason).HasMaxLength(2048);
            b.Property(x => x.SuggestedName).HasMaxLength(256);
            b.Property(x => x.NameEditedByUser).HasDefaultValue(false);
            b.Property(x => x.PairsJson).HasColumnType("text");
            b.Property(x => x.AutoCompareOnParseComplete).HasDefaultValue(true);
            b.Property(x => x.TenderReadingTaskId);
            b.Property(x => x.TenderReadingBaselineVersion);
            b.HasIndex(x => x.Status);
        });

        builder.Entity<CompareDocument>(b =>
        {
            b.ToTable("BcCompareDocuments");
            b.ConfigureByConvention();
            b.Property(x => x.FileName).IsRequired().HasMaxLength(256);
            b.Property(x => x.FileExtension).IsRequired().HasMaxLength(16);
            b.Property(x => x.OriginStorageKey).IsRequired().HasMaxLength(512);
            b.Property(x => x.IrStorageKey).HasMaxLength(512);
            b.Property(x => x.DocMdStorageKey).HasMaxLength(512);
            b.Property(x => x.AnGineerDocId).HasMaxLength(128);
            b.Property(x => x.ParseError).HasMaxLength(2048);
            b.Property(x => x.ParseStage).HasMaxLength(64);
            b.Property(x => x.ParseStageMessage).HasMaxLength(1024);
            b.HasIndex(x => x.TaskId);
        });

        builder.Entity<CompareDraftDocument>(b =>
        {
            b.ToTable("BcCompareDraftDocuments");
            b.ConfigureByConvention();
            b.Property(x => x.FileName).IsRequired().HasMaxLength(256);
            b.Property(x => x.FileExtension).IsRequired().HasMaxLength(16);
            b.Property(x => x.OriginStorageKey).IsRequired().HasMaxLength(512);
            b.HasIndex(x => x.DraftId);
        });

        builder.Entity<EvidenceItem>(b =>
        {
            b.ToTable("BcEvidenceItems");
            b.ConfigureByConvention();
            b.Property(x => x.DocIdsJson).IsRequired().HasColumnType("text");
            b.Property(x => x.LocationsJson).IsRequired().HasColumnType("text");
            b.Property(x => x.MetricsJson).HasColumnType("text");
            b.Property(x => x.Title).IsRequired().HasMaxLength(512);
            b.Property(x => x.Description).IsRequired().HasMaxLength(4000);
            b.HasIndex(x => new { x.TaskId, x.Type });
            b.HasIndex(x => new { x.TaskId, x.Severity });
        });

        builder.Entity<ClauseTemplate>(b =>
        {
            b.ToTable("BcClauseTemplates");
            b.ConfigureByConvention();
            b.Property(x => x.Text).IsRequired().HasMaxLength(2000);
            b.Property(x => x.Category).HasMaxLength(64);
        });

        builder.Entity<ExportJob>(b =>
        {
            b.ToTable("BcExportJobs");
            b.ConfigureByConvention();
            b.Property(x => x.FileStorageKey).HasMaxLength(512);
            b.Property(x => x.Error).HasMaxLength(2048);
            b.HasIndex(x => x.TaskId);
        });

        builder.Entity<AiUsageRecord>(b =>
        {
            b.ToTable("BcAiUsageRecords");
            b.ConfigureByConvention();
            b.Property(x => x.Business).IsRequired().HasMaxLength(64);
            b.Property(x => x.UsedConfig).IsRequired().HasMaxLength(128);
            b.Property(x => x.UsedModel).IsRequired().HasMaxLength(128);
            b.Property(x => x.ErrorMessage).HasMaxLength(2048);
            b.Property(x => x.TextPreview).HasMaxLength(512);
            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.UsedConfig);
            b.HasIndex(x => x.Business);
            b.HasIndex(x => new { x.Success, x.CreationTime });
        });

        builder.Entity<TenderReadingTask>(b =>
        {
            b.ToTable("BcTenderReadingTasks");
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.ProjectCode).HasMaxLength(64);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.ProgressStage).IsRequired().HasMaxLength(32);
            b.Property(x => x.FailureReason).HasMaxLength(2048);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.ProjectCode);
        });

        builder.Entity<TenderReadingDocument>(b =>
        {
            b.ToTable("BcTenderReadingDocuments");
            b.ConfigureByConvention();
            b.Property(x => x.FileName).IsRequired().HasMaxLength(256);
            b.Property(x => x.FileExtension).IsRequired().HasMaxLength(16);
            b.Property(x => x.OriginStorageKey).IsRequired().HasMaxLength(512);
            b.Property(x => x.AnGineerDocId).HasMaxLength(128);
            b.Property(x => x.IrStorageKey).HasMaxLength(512);
            b.Property(x => x.DocMdStorageKey).HasMaxLength(512);
            b.Property(x => x.ParseError).HasMaxLength(2048);
            b.Property(x => x.ParseStage).HasMaxLength(64);
            b.Property(x => x.ParseStageMessage).HasMaxLength(1024);
            b.HasIndex(x => x.TaskId);
        });

        builder.Entity<BaselineField>(b =>
        {
            b.ToTable("BcBaselineFields");
            b.ConfigureByConvention();
            b.Property(x => x.FieldKey).IsRequired().HasMaxLength(128);
            b.Property(x => x.ValueJson).IsRequired().HasColumnType("text");
            b.Property(x => x.RawText).IsRequired().HasColumnType("text");
            b.Property(x => x.Confidence).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.Extractor).IsRequired().HasMaxLength(32);
            b.Property(x => x.ExtractorVersion).IsRequired().HasMaxLength(32);
            b.HasIndex(x => new { x.TaskId, x.Category });
            b.HasIndex(x => new { x.TaskId, x.FieldKey });
        });

        builder.Entity<SourceMapItem>(b =>
        {
            b.ToTable("BcSourceMapItems");
            b.ConfigureByConvention();
            b.Property(x => x.BlockId).IsRequired().HasMaxLength(128);
            b.Property(x => x.BboxJson).IsRequired().HasColumnType("text");
            b.Property(x => x.Text).IsRequired().HasColumnType("text");
            b.HasIndex(x => x.FieldId);
            b.HasIndex(x => new { x.FieldId, x.PageIdx });
        });

        builder.Entity<MeetingRecord>(b =>
        {
            b.ToTable("BcMeetingRecords");
            b.ConfigureByConvention();
            b.Property(x => x.PreInfoJson).IsRequired().HasColumnType("text");
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.TranscriptFile).HasMaxLength(512);
            b.Property(x => x.TranscriptText).HasColumnType("text");
            b.Property(x => x.ReportFile).HasMaxLength(512);
            b.Property(x => x.ReportError).HasMaxLength(2048);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.Date);
        });

        builder.Entity<SpeechDraft>(b =>
        {
            b.ToTable("BcSpeechDrafts");
            b.ConfigureByConvention();
            b.Property(x => x.Content).IsRequired().HasColumnType("text");
            b.Property(x => x.Status).IsRequired().HasMaxLength(16);
            b.HasIndex(x => x.MeetingRecordId).IsUnique();
        });

        builder.Entity<AttendanceRecord>(b =>
        {
            b.ToTable("BcAttendanceRecords");
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(64);
            b.Property(x => x.Team).HasMaxLength(64);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.Confidence).IsRequired();
            b.HasIndex(x => x.MeetingRecordId);
            b.HasIndex(x => new { x.MeetingRecordId, x.WorkerId });
        });

        builder.Entity<QaRecord>(b =>
        {
            b.ToTable("BcQaRecords");
            b.ConfigureByConvention();
            b.Property(x => x.Question).IsRequired().HasColumnType("text");
            b.Property(x => x.Answer).IsRequired().HasColumnType("text");
            b.Property(x => x.IntentType).IsRequired();
            b.Property(x => x.SourcesJson).IsRequired().HasColumnType("text");
            b.HasIndex(x => x.MeetingRecordId);
        });

        builder.Entity<WorkerProfile>(b =>
        {
            b.ToTable("BcWorkerProfiles");
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(64);
            b.Property(x => x.EmployeeNo).IsRequired().HasMaxLength(32);
            b.Property(x => x.Team).HasMaxLength(64);
            b.Property(x => x.FaceStatus).IsRequired();
            b.Property(x => x.FacePhotosJson).IsRequired().HasColumnType("text");
            b.HasIndex(x => x.EmployeeNo).IsUnique();
        });
    }
}

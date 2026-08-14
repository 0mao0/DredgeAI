using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
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
    public DbSet<EvidenceItem> EvidenceItems { get; set; }
    public DbSet<ClauseTemplate> ClauseTemplates { get; set; }
    public DbSet<ExportJob> ExportJobs { get; set; }

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
            b.Property(x => x.ReportJson).HasColumnType("text");
            b.Property(x => x.ProgressStage).IsRequired().HasMaxLength(32);
            b.Property(x => x.ProgressMessage).HasMaxLength(1024);
            b.Property(x => x.FailureReason).HasMaxLength(2048);
            b.Property(x => x.SuggestedName).HasMaxLength(256);
            b.Property(x => x.NameEditedByUser).HasDefaultValue(false);
            b.Property(x => x.PairsJson).HasColumnType("text");
            b.Property(x => x.AutoCompareOnParseComplete).HasDefaultValue(true);
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
            b.Property(x => x.ParseError).HasMaxLength(2048);
            b.HasIndex(x => x.TaskId);
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
    }
}

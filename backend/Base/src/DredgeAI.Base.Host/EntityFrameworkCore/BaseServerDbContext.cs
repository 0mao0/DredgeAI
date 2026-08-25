using Microsoft.EntityFrameworkCore;
using Shiw.Abp.AuditLogging.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Shiw.Abp.FeatureManagement.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Shiw.Abp.PermissionManagement.EntityFrameworkCore;
using Shiw.Abp.SettingManagement.EntityFrameworkCore;
using Shiw.Abp.TenantManagement.EntityFrameworkCore;
using Shiw.File.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI.EntityFrameworkCore;

public class BaseServerDbContext:AbpDbContext<BaseServerDbContext>
{
    private readonly IShiwDbContextHandler _handler;

    public BaseServerDbContext(DbContextOptions<BaseServerDbContext> options,IShiwDbContextHandler handler) : base(options)
    {
        _handler = handler;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureShiwPermissionManagement(_handler);
        modelBuilder.ConfigureShiwSettingManagement(_handler);
        modelBuilder.ConfigureShiwAuditLogging(_handler);
        modelBuilder.ConfigureShiwIdentity(_handler);
        modelBuilder.ConfigureShiwFeatureManagement(_handler);
        modelBuilder.ConfigureShiwTenantManagement(_handler);
        modelBuilder.ConfigureShiwFile(_handler);
        modelBuilder.ConfigureDredgeAIBase(_handler);
    }
    
}
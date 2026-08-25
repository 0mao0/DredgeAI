using Microsoft.EntityFrameworkCore;
using Shiw.Abp.AuditLogging.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Shiw.Abp.FeatureManagement.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Shiw.Abp.OpenIddict.EntityFrameworkCore;
using Shiw.Abp.PermissionManagement.EntityFrameworkCore;
using Shiw.Abp.SettingManagement.EntityFrameworkCore;
using Shiw.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace DredgeAI.EntityFrameworkCore;

public class AuthServerDbContext:AbpDbContext<AuthServerDbContext>
{
    private readonly IShiwDbContextHandler _handler;

    public AuthServerDbContext(DbContextOptions<AuthServerDbContext> options,IShiwDbContextHandler handler) : base(options)
    {
        _handler = handler;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureShiwOpenIddict(_handler);
    }
    
}
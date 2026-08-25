using Microsoft.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI;

[ConnectionStringName(DredgeAIBaseDbProperties.ConnectionStringName)]
public class DredgeAIBaseDbContext : AbpDbContext<DredgeAIBaseDbContext>, IDredgeAIBaseDbContext
{
    private readonly IShiwDbContextHandler _handler;

    public DbSet<DictType> DictTypes { get; set; }
    public DbSet<DictData> DictDatas { get; set; }
    public DbSet<MenuInfo> Menus { get; set; }
    public DbSet<IdentityUserExtension> UserExtensions { get; set; }

    public DredgeAIBaseDbContext(DbContextOptions<DredgeAIBaseDbContext> options, IShiwDbContextHandler handler)
        : base(options)
    {
        _handler = handler;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureDredgeAIBase(_handler);
    }
}

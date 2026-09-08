using DredgeAI.Gateway.Proxying;
using Microsoft.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI.Gateway.EntityFrameworkCore;

[ConnectionStringName(GatewayDbProperties.ConnectionStringName)]
public class GatewayDbContext : AbpDbContext<GatewayDbContext>
{
    public DbSet<ProxyRoute> ProxyRoutes { get; set; }
    public DbSet<ProxyCluster> ProxyClusters { get; set; }

    private readonly IShiwDbContextHandler _handler;

    public GatewayDbContext(
        DbContextOptions<GatewayDbContext> options,
        IShiwDbContextHandler handler)
        : base(options)
    {
        _handler = handler;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureGateway(_handler);
    }
}

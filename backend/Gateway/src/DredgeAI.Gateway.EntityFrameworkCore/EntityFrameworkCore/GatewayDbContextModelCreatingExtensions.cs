using DredgeAI.Gateway.Proxying;
using Microsoft.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp;

namespace DredgeAI.Gateway.EntityFrameworkCore;

public static class GatewayDbContextModelCreatingExtensions
{
    public static void ConfigureGateway(
        this ModelBuilder builder, IShiwDbContextHandler handler)
    {
        Check.NotNull(builder, nameof(builder));

        // ProxyRoute — YARP 代理路由
        builder.Entity<ProxyRoute>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{GatewayDbProperties.DbTablePrefix}{nameof(ProxyRoute)}"),
                GatewayDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.RouteId)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.RouteId)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("YARP 路由 ID，全局唯一");
            b.HasIndex(x => x.RouteId).IsUnique();

            b.Property(x => x.ClusterId)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.ClusterId)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("引用的集群 ID");

            b.Property(x => x.Order)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.Order)))
                .IsRequired()
                .HasComment("路由匹配优先级（值越小越优先）");

            b.Property(x => x.ConfigJson)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.ConfigJson)))
                .HasColumnType("text")
                .IsRequired()
                .HasComment("完整 YARP RouteConfig JSON（camelCase）");

            b.Property(x => x.IsEnabled)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.IsEnabled)))
                .IsRequired()
                .HasComment("是否启用；禁用的路由不进 YARP 快照");

            b.Property(x => x.Description)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.Description)))
                .HasMaxLength(256)
                .HasComment("路由描述");
        });

        // ProxyCluster — YARP 代理集群
        builder.Entity<ProxyCluster>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{GatewayDbProperties.DbTablePrefix}{nameof(ProxyCluster)}"),
                GatewayDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.ClusterId)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyCluster.ClusterId)))
                .IsRequired()
                .HasMaxLength(128)
                .HasComment("YARP 集群 ID，全局唯一");
            b.HasIndex(x => x.ClusterId).IsUnique();

            b.Property(x => x.ConfigJson)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyCluster.ConfigJson)))
                .HasColumnType("text")
                .IsRequired()
                .HasComment("完整 YARP ClusterConfig JSON（camelCase）");

            b.Property(x => x.Description)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyCluster.Description)))
                .HasMaxLength(256)
                .HasComment("集群描述");

            b.Property(x => x.IsEnabled)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyCluster.IsEnabled)))
                .IsRequired()
                .HasDefaultValue(true)
                .HasComment("是否启用；禁用的集群及其路由不进 YARP 快照");
        });
    }
}

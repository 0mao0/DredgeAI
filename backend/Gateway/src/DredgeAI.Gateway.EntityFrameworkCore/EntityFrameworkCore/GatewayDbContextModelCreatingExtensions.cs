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

            b.Property(x => x.MatchPath)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.MatchPath)))
                .IsRequired()
                .HasMaxLength(256)
                .HasComment("路径匹配模式，如 /api/compare/{**catch-all}");

            b.Property(x => x.MatchHostsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.MatchHostsJson)))
                .HasColumnType("text")
                .HasComment("Host 匹配列表 JSON 数组，null 表示不限制");

            b.Property(x => x.MatchMethodsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.MatchMethodsJson)))
                .HasColumnType("text")
                .HasComment("HTTP 方法匹配列表 JSON 数组，null 表示不限制");

            b.Property(x => x.AuthorizationPolicy)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.AuthorizationPolicy)))
                .IsRequired()
                .HasMaxLength(64)
                .HasComment("授权策略（YARP 内置字面量 anonymous 或 default）");

            b.Property(x => x.IsEnabled)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyRoute.IsEnabled)))
                .IsRequired()
                .HasComment("是否启用；禁用的路由不进 YARP 快照");
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

            b.Property(x => x.DestinationsJson)
                .HasColumnName(handler.FieldNameHandler(nameof(ProxyCluster.DestinationsJson)))
                .IsRequired()
                .HasColumnType("text")
                .HasComment("目的地字典 JSON（destinationId → 下游地址）");
        });
    }
}

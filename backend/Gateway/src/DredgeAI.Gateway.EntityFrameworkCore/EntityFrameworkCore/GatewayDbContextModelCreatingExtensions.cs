using DredgeAI.Gateway.Proxying;
using DredgeAI.Gateway.RateLimiting;
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

        // RateLimitPolicy — 限流策略（全局/路由级，固定窗口/滑动窗口/令牌桶）
        builder.Entity<RateLimitPolicy>(b =>
        {
            b.ToTable(
                handler.TableNameHandler($"{GatewayDbProperties.DbTablePrefix}{nameof(RateLimitPolicy)}"),
                GatewayDbProperties.DbSchema);
            b.ConfigureByConvention(handler);

            b.Property(x => x.Name)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.Name)))
                .IsRequired()
                .HasMaxLength(RateLimitPolicyConsts.MaxNameLength)
                .HasComment("显示名，全局唯一");

            b.Property(x => x.Scope)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.Scope)))
                .IsRequired()
                .HasComment("作用域：0=全局，1=路由级");

            b.Property(x => x.RouteId)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.RouteId)))
                .HasMaxLength(RateLimitPolicyConsts.MaxRouteIdLength)
                .HasComment("目标路由 ID（Scope=Route 时必填）");
            b.HasIndex(x => new { x.Scope, x.RouteId });

            b.Property(x => x.Algorithm)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.Algorithm)))
                .IsRequired()
                .HasComment("限流算法：0=固定窗口，1=滑动窗口，2=令牌桶");

            b.Property(x => x.PermitLimit)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.PermitLimit)))
                .HasComment("窗口内允许的请求数（Fixed/Sliding 必填）");

            b.Property(x => x.WindowSeconds)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.WindowSeconds)))
                .HasComment("窗口时长秒数（Fixed/Sliding 必填）");

            b.Property(x => x.SegmentsPerWindow)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.SegmentsPerWindow)))
                .HasComment("滑动窗口分段数（Sliding 必填）");

            b.Property(x => x.TokenLimit)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.TokenLimit)))
                .HasComment("令牌桶容量（TokenBucket 必填）");

            b.Property(x => x.TokensPerPeriod)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.TokensPerPeriod)))
                .HasComment("每周期补充令牌数（TokenBucket 必填）");

            b.Property(x => x.ReplenishmentPeriodSeconds)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.ReplenishmentPeriodSeconds)))
                .HasComment("令牌补充周期秒数（TokenBucket 必填）");

            b.Property(x => x.QueueLimit)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.QueueLimit)))
                .IsRequired()
                .HasDefaultValue(0)
                .HasComment("排队上限（>=0，默认 0 不排队）");

            b.Property(x => x.IsEnabled)
                .HasColumnName(handler.FieldNameHandler(nameof(RateLimitPolicy.IsEnabled)))
                .IsRequired()
                .HasDefaultValue(true)
                .HasComment("是否启用；禁用的策略不参与限流解析");
            
            var rateLimitPolicyIndex = b.HasIndex(x => x.Name).IsUnique();
            if (typeof(ISoftDelete).IsAssignableFrom(typeof(RateLimitPolicy)))
            {
                var filter = $"{handler.FieldNameHandler(nameof(ISoftDelete.IsDeleted))} = false";
                rateLimitPolicyIndex.HasFilter(filter); }
            
        });
    }
}

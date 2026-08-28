namespace DredgeAI.AppHost;

/// <summary>
/// 本地运行分层编排开关。本地运行缺省 Backend；发布模式由调用方传 OrchestrationTier.All 作缺省（始终注册全部资源，可用 -- --tier= 收窄）。
/// 用法：dotnet run --launch-profile python|frontend|all，或 dotnet run -- --tier=backend,frontend（逗号组合）。
/// </summary>
[Flags]
public enum OrchestrationTier
{
    Backend = 1,   // 4 个 .NET 后端（auth/base/bidcompare/gateway）
    Python = 2,    // services/ 下 7 个 Python 服务
    Frontend = 4,  // user-web / admin-web
    All = Backend | Python | Frontend
}

public static class OrchestrationTierResolver
{
    /// <summary>解析 --tier 参数（命令行 args 自动进入 Configuration）。空/缺省 = defaultTier；"all" = All；逗号分隔子集（大小写不敏感）；非法值抛 InvalidOperationException 并列出合法值。</summary>
    public static OrchestrationTier Resolve(IDistributedApplicationBuilder builder, OrchestrationTier defaultTier)
    {
        var raw = builder.Configuration["tier"];
        if (string.IsNullOrWhiteSpace(raw)) return defaultTier;
        if (string.Equals(raw.Trim(), "all", StringComparison.OrdinalIgnoreCase)) return OrchestrationTier.All;

        var result = default(OrchestrationTier);
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<OrchestrationTier>(part, ignoreCase: true, out var t) || t is < OrchestrationTier.Backend or > OrchestrationTier.Frontend)
                throw new InvalidOperationException($"非法 tier 值 '{part}'。合法值：backend | python | frontend | all（逗号组合，如 backend,python）。");
            result |= t;
        }
        return result == 0 ? defaultTier : result;
    }
}

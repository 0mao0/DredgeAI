namespace DredgeAI.BidCompare.Applications;

/// <summary>应用排序级别。</summary>
public enum AppOrderLevel : byte
{
    /// <summary>全局默认顺序（admin 维护）。</summary>
    Global = 0,

    /// <summary>用户个性化顺序。</summary>
    User = 1
}

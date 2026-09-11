namespace DredgeAI.BidCompare.Applications;

/// <summary>应用状态：主应用用 Online/Offline，子应用用 Published/Unpublished。</summary>
public enum AppCatalogStatus : byte
{
    /// <summary>运营中（主应用）。</summary>
    Online = 0,

    /// <summary>已下架（主应用）。</summary>
    Offline = 1,

    /// <summary>已发布（子应用）。</summary>
    Published = 2,

    /// <summary>已下架（子应用）。</summary>
    Unpublished = 3
}

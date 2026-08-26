namespace DredgeAI.BidCompare.Exports;

public class LibreOfficeOptions
{
    /// <summary>soffice 可执行文件路径（PATH 中则直接 "soffice"）。</summary>
    public string SofficePath { get; set; } = "soffice";

    public int TimeoutSeconds { get; set; } = 180;
}

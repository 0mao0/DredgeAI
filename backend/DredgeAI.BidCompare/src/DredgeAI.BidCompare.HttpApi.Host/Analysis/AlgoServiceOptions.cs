namespace DredgeAI.BidCompare.Analysis;

public class AlgoServiceOptions
{
    /// <summary>Python 算法服务基地址，如 http://localhost:8900。</summary>
    public string BaseUrl { get; set; } = "http://localhost:8900";

    /// <summary>单次请求超时（秒）。多份 100~500 页标书比对耗时长，默认 10 分钟。</summary>
    public int TimeoutSeconds { get; set; } = 600;
}

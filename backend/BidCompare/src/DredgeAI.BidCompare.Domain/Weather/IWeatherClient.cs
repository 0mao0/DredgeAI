using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Weather;

/// <summary>
/// 天气查询（外部免费数据源，如 wttr.in）。查询失败或城市为空时返回空字符串，上层降级。
/// </summary>
public interface IWeatherClient
{
    Task<string> GetWeatherTextAsync(string city, CancellationToken cancellationToken = default);
}

public class WeatherOptions
{
    /// <summary>默认城市兜底（未配置时留空，不查询）。</summary>
    public string? DefaultCity { get; set; }
}

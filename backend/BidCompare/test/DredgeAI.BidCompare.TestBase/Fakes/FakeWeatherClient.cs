using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Weather;

namespace DredgeAI.BidCompare;

/// <summary>天气可编程 Fake：默认返回固定天气文本，可模拟查询失败。</summary>
public class FakeWeatherClient : IWeatherClient
{
    public string WeatherText { get; set; } = "多云 26℃";

    public bool ThrowOnQuery { get; set; }

    public List<string> QueriedCities { get; } = [];

    public Task<string> GetWeatherTextAsync(string city, CancellationToken cancellationToken = default)
    {
        QueriedCities.Add(city);
        if (ThrowOnQuery)
        {
            throw new InvalidOperationException("weather service down");
        }
        return Task.FromResult(WeatherText);
    }
}

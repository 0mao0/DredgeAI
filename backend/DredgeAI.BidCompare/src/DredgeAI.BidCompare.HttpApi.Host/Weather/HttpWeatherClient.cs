using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Weather;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.BidCompare.Weather;

/// <summary>
/// wttr.in 天气客户端（免费、免 key）：GET https://wttr.in/{city}?format=j1&amp;lang=zh，
/// 返回"天气描述 温度℃"；任何失败均返回空字符串供上层降级。
/// </summary>
public class HttpWeatherClient : IWeatherClient, ITransientDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WeatherOptions _options;
    private readonly ILogger<HttpWeatherClient> _logger;

    public HttpWeatherClient(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherOptions> options,
        ILogger<HttpWeatherClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetWeatherTextAsync(string city, CancellationToken cancellationToken = default)
    {
        var resolved = string.IsNullOrWhiteSpace(city) ? _options.DefaultCity : city;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return "";
        }
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(HttpWeatherClient));
            var url = $"https://wttr.in/{Uri.EscapeDataString(resolved.Trim())}?format=j1&lang=zh";
            var payload = await client.GetFromJsonAsync<WttrPayload>(
                url,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            var current = payload?.CurrentCondition?.FirstOrDefault();
            if (current is null)
            {
                return "";
            }
            var desc = current.WeatherDesc?.FirstOrDefault()?.Value ?? "";
            var temp = current.TempC ?? "";
            return string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(temp)
                ? ""
                : string.Join(" ", new[] { desc, temp }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "wttr.in 天气查询失败：{City}", resolved);
            return "";
        }
    }

    private class WttrPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("current_condition")]
        public List<CurrentCondition>? CurrentCondition { get; set; }
    }

    private class CurrentCondition
    {
        [System.Text.Json.Serialization.JsonPropertyName("temp_C")]
        public string? TempC { get; set; }

        public List<WeatherDesc>? WeatherDesc { get; set; }
    }

    private class WeatherDesc
    {
        public string? Value { get; set; }
    }
}

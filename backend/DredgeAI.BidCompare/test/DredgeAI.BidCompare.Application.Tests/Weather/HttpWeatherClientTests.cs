using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DredgeAI.BidCompare.Weather;

public class HttpWeatherClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;
        public string? LastUrl { get; private set; }

        public StubHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static HttpWeatherClient CreateClient(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(HttpWeatherClient))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        return new HttpWeatherClient(
            factory,
            Options.Create(new WeatherOptions()),
            NullLogger<HttpWeatherClient>.Instance);
    }

    [Fact]
    public async Task GetWeatherTextAsync_Should_Parse_Current_Condition()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            "{\"current_condition\":[{\"temp_C\":\"28\",\"weatherDesc\":[{\"value\":\"Partly Cloudy\"}]}]}");
        var client = CreateClient(handler);

        var text = await client.GetWeatherTextAsync("上海");

        Assert.Equal("Partly Cloudy 28", text);
        Assert.NotNull(handler.LastUrl);
        Assert.Contains("wttr.in", handler.LastUrl);
    }

    [Fact]
    public async Task GetWeatherTextAsync_Should_Return_Empty_When_City_Empty()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.OK, "{}"));

        var text = await client.GetWeatherTextAsync("");

        Assert.Equal("", text);
    }

    [Fact]
    public async Task GetWeatherTextAsync_Should_Return_Empty_When_Service_Fails()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.InternalServerError, "boom"));

        var text = await client.GetWeatherTextAsync("上海");

        Assert.Equal("", text);
    }
}

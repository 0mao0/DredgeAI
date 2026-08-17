using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Xunit;

namespace DredgeAI.BidCompare.AI;

public class HttpLlmGatewayTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private static HttpLlmGateway CreateGateway(HttpMessageHandler handler, string token = "")
    {
        var services = new ServiceCollection();
        services.AddHttpClient(nameof(HttpLlmGateway))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .ConfigureHttpClient(c => c.BaseAddress = new System.Uri("http://gateway.test/"));
        var factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var options = Options.Create(new AiGatewayOptions { ApiToken = token });
        return new HttpLlmGateway(factory, options, NullLogger<HttpLlmGateway>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_Returns_Text_From_Gateway()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"text\":\"条款：...\",\"finishReason\":\"stop\",\"attempts\":1,\"latencySeconds\":0.5}",
                System.Text.Encoding.UTF8,
                "application/json")
        };
        var handler = new StubHandler(response);
        var gateway = CreateGateway(handler);

        var text = await gateway.CompleteAsync("system", "user");

        Assert.Equal("条款：...", text);
        Assert.Equal("http://gateway.test/v1/chat", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_Throws_With_Service_Code_On_502()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("{\"code\":\"PROVIDER_UNAVAILABLE\",\"message\":\"all down\"}")
        };
        var gateway = CreateGateway(new StubHandler(response));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => gateway.CompleteAsync("system", "user"));
        Assert.Equal(BidCompareErrorCodes.AiGatewayFailed, ex.Code);
        Assert.Equal("PROVIDER_UNAVAILABLE", ex.Data["serviceCode"]);
    }
}

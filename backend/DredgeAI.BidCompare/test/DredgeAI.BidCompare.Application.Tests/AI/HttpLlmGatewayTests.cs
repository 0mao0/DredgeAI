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
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public StubHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // 瞬时错误会触发重试，每次调用必须返回全新响应实例（上一实例已被释放）
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            });
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
        var handler = new StubHandler(HttpStatusCode.OK,
            "{\"text\":\"条款：...\",\"finishReason\":\"stop\",\"attempts\":1,\"latencySeconds\":0.5}");
        var gateway = CreateGateway(handler);

        var text = await gateway.CompleteAsync("system", "user");

        Assert.Equal("条款：...", text);
        Assert.Equal("http://gateway.test/v1/chat", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_Throws_With_Service_Code_On_502()
    {
        var handler = new StubHandler(HttpStatusCode.BadGateway, "{\"code\":\"PROVIDER_UNAVAILABLE\",\"message\":\"all down\"}");
        var gateway = CreateGateway(handler);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => gateway.CompleteAsync("system", "user"));
        Assert.Equal(BidCompareErrorCodes.AiGatewayFailed, ex.Code);
        Assert.Equal("PROVIDER_UNAVAILABLE", ex.Data["serviceCode"]);
        // 502 为瞬时错误：先按 TransientHttpRetry 重试，最后一次尝试保留错误信封
        Assert.Equal(3, handler.CallCount);
    }
}

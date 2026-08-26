using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace DredgeAI.BidCompare.AnGineer;

/// <summary>
/// HttpAnGineerClient 提交重试回归：首次瞬时失败后重试必须重新打开流工厂，
/// 旧实现（复用同一 Stream + StreamContent dispose）会在重试时报 "Cannot access a closed file"。
/// </summary>
public class HttpAnGineerClientSubmitTests
{
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> _respond;
        private int _calls;

        public SequenceHandler(Func<int, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public int CallCount => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            return Task.FromResult(_respond(call));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8790") };
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private static HttpAnGineerClient BuildClient(SequenceHandler handler)
    {
        return new HttpAnGineerClient(
            new StubHttpClientFactory(handler),
            Options.Create(new AnGineerOptions { BaseUrl = "http://localhost:8790" }),
            NullLogger<HttpAnGineerClient>.Instance);
    }

    [Fact]
    public async Task Submit_Should_Retry_With_Fresh_Stream_After_Transient_Failure()
    {
        var handler = new SequenceHandler(call =>
        {
            if (call == 1)
            {
                // 首次：500（瞬时），确保 try 块内 StreamContent/Form 释放链执行
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"doc_id\":\"doc-123\"}", Encoding.UTF8, "application/json"),
            };
        });
        var client = BuildClient(handler);

        var opened = 0;
        var result = await client.SubmitAsync("标书A.pdf", async () =>
        {
            opened++;
            await Task.CompletedTask;
            return new MemoryStream(Encoding.UTF8.GetBytes("%PDF-bytes"));
        });

        result.ShouldBe("doc-123");
        handler.CallCount.ShouldBe(2);
        opened.ShouldBe(2); // 每次重试都重新打开流，未复用已关闭流
    }

    [Fact]
    public async Task Submit_Should_Fail_When_NonTransient_Error()
    {
        var handler = new SequenceHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = BuildClient(handler);

        var ex = await Should.ThrowAsync<HttpRequestException>(() =>
            client.SubmitAsync("标书A.pdf", () => Task.FromResult<Stream>(
                new MemoryStream(Encoding.UTF8.GetBytes("%PDF-bytes")))));

        ex.Message.ShouldContain("401");
        handler.CallCount.ShouldBe(1); // 401 非瞬时，不重试
    }
}

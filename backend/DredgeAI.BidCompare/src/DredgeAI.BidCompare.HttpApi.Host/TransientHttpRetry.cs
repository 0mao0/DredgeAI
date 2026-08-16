using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DredgeAI.BidCompare;

/// <summary>
/// 轻量瞬时 HTTP 错误重试（不引 Polly 依赖）：5xx / 408 / 429 / 连接重置 / 客户端超时，
/// 指数退避（0.5s、1s、2s...），最多 maxAttempts 次。4xx 业务错误不重试。
/// </summary>
public static class TransientHttpRetry
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        ILogger logger,
        string operation,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex, cancellationToken))
            {
                var delay = Backoff(attempt);
                logger.LogWarning(ex, "{Operation} 瞬时失败（第 {Attempt}/{MaxAttempts} 次），{Delay}ms 后重试",
                    operation, attempt, maxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>指数退避：0.5s、1s、2s...</summary>
    public static TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));

    public static bool IsTransient(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is >= HttpStatusCode.InternalServerError
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests)
            {
                return true;
            }
            for (var inner = httpEx.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is IOException or SocketException)
                {
                    return true;
                }
            }
            return false;
        }
        // 客户端自身超时（HttpClient.Timeout）表现为 TaskCanceledException 且非外部取消
        return ex is TaskCanceledException && !cancellationToken.IsCancellationRequested;
    }
}

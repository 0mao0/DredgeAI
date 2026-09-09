using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>AI 网关接口（用量上报）</summary>
/// <remarks>本端点不启用 [Authorize]：鉴权依赖请求头 X-Gateway-Token 与服务端配置的 AI_GATEWAY_INGEST_TOKEN 比对；
/// 未配置令牌或令牌不匹配时一律拒绝（fail-closed）。</remarks>
[Route("api/bidcompare/ai-gateway")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("AI 网关")]
public class AiGatewayController : BidCompareController
{
    private readonly IAiUsageRecordAppService _usageAppService;
    private readonly AiGatewayOptions _options;

    public AiGatewayController(
        IAiUsageRecordAppService usageAppService,
        IOptions<AiGatewayOptions> options)
    {
        _usageAppService = usageAppService;
        _options = options.Value;
    }

    /// <summary>AI 网关用量上报（POST /api/bidcompare/ai-gateway/usage-records）</summary>
    /// <remarks>X-Gateway-Token 校验；未配置令牌时 fail-closed 拒绝。</remarks>
    /// <param name="input">用量记录数据</param>
    /// <returns>创建成功的用量记录</returns>
    [HttpPost("usage-records")]
    public async Task<AiUsageRecordDto> CreateUsageRecordAsync([FromBody] CreateAiUsageRecordDto input)
    {
        if (string.IsNullOrWhiteSpace(_options.IngestToken))
        {
            throw new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
                .WithData("reason", "服务端未配置 AI_GATEWAY_INGEST_TOKEN，用量上报端点已禁用");
        }
        if (Request.Headers["X-Gateway-Token"] != _options.IngestToken)
        {
            throw new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
                .WithData("reason", "无效的网关上报令牌");
        }
        return await _usageAppService.CreateAsync(input);
    }
}

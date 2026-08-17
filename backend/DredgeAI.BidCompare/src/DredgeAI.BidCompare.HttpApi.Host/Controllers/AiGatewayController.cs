using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Route("api/ai-gateway")]
public class AiGatewayController : AbpControllerBase
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

    /// <summary>POST /api/ai-gateway/usage-records 网关用量上报（X-Gateway-Token 校验）。</summary>
    [HttpPost("usage-records")]
    public async Task<AiUsageRecordDto> CreateUsageRecordAsync([FromBody] CreateAiUsageRecordDto input)
    {
        if (!string.IsNullOrWhiteSpace(_options.IngestToken)
            && Request.Headers["X-Gateway-Token"] != _options.IngestToken)
        {
            throw new BusinessException(BidCompareErrorCodes.AiGatewayFailed)
                .WithData("reason", "无效的网关上报令牌");
        }
        return await _usageAppService.CreateAsync(input);
    }
}

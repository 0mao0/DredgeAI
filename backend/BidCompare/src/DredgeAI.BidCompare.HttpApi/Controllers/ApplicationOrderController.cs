using System.Threading.Tasks;
using DredgeAI.BidCompare.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// 用户个性化应用顺序（DB 持久化）：
/// 前端合并规则——个性化优先，未个性化用户按目录返回的全局顺序，新应用按全局顺序稳定插入。
/// </summary>
[Authorize]
[Route("api/bidcompare/app-order")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("应用排序")]
public class ApplicationOrderController : BidCompareController
{
    private readonly IUserAppOrderAppService _userAppOrderAppService;

    public ApplicationOrderController(IUserAppOrderAppService userAppOrderAppService)
    {
        _userAppOrderAppService = userAppOrderAppService;
    }

    /// <summary>GET /api/bidcompare/app-order/user 获取当前用户的个性化顺序（route 列表；未个性化返回 null）</summary>
    /// <returns>当前用户的个性化顺序</returns>
    [HttpGet("user")]
    public Task<UserApplicationOrderResult> GetUserOrderAsync()
        => _userAppOrderAppService.GetUserOrderAsync();

    /// <summary>PUT /api/bidcompare/app-order/user 保存当前用户的个性化顺序（route 列表）</summary>
    /// <param name="input">route 顺序列表</param>
    /// <returns>保存后的个性化顺序</returns>
    [HttpPut("user")]
    public Task<UserApplicationOrderResult> SetUserOrderAsync([FromBody] SetUserApplicationOrderInput input)
        => _userAppOrderAppService.SetUserOrderAsync(input);

    /// <summary>POST /api/bidcompare/app-order/reset 清空所有用户的个性化顺序（管理员显式动作）</summary>
    /// <returns>清空的数量</returns>
    [HttpPost("reset")]
    public Task<ResetUserOrdersResult> ResetAsync()
        => _userAppOrderAppService.ResetUserOrdersAsync();
}

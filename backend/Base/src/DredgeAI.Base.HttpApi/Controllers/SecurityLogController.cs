using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.Permissions;
using DredgeAI.SecurityLogs;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>安全日志管理接口</summary>
/// <remarks>基于 ABP Identity 安全日志实体，只提供只读列表查询能力</remarks>
[Authorize]
[Route("api/base/security-logs")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("安全日志")]
public class SecurityLogController : DredgeAIBaseController, ISecurityLogAppService
{
    private readonly ISecurityLogAppService _service;

    public SecurityLogController(ISecurityLogAppService service)
    {
        _service = service;
    }

    /// <summary>分页查询安全日志列表</summary>
    /// <param name="input">查询条件，支持时间范围、用户、动作、身份、客户端 IP 过滤</param>
    /// <returns>分页的安全日志列表</returns>
    [HttpGet]
    [Authorize(DredgeAIBasePermissions.SecurityLogs.Default)]
    public Task<PagedResultDto<SecurityLogListItemDto>> GetListAsync([FromQuery] GetSecurityLogListInput input)
        => _service.GetListAsync(input);
}

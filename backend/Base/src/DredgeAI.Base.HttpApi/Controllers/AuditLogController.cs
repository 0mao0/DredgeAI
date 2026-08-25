using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.AuditLogs;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>审计日志管理接口</summary>
/// <remarks>基于 ABP 审计日志实体，只提供只读查询能力</remarks>
[Authorize]
[Route("api/base/audit-logs")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("审计日志")]
public class AuditLogController : DredgeAIBaseController, IAuditLogAppService
{
    private readonly IAuditLogAppService _service;

    public AuditLogController(IAuditLogAppService service)
    {
        _service = service;
    }

    /// <summary>分页查询审计日志列表</summary>
    /// <param name="input">查询条件，支持时间范围、用户、HTTP 方法、URL、状态码、异常状态过滤</param>
    /// <returns>分页的审计日志列表</returns>
    [HttpGet]
    [Authorize(DredgeAIBasePermissions.AuditLogs.Default)]
    public Task<PagedResultDto<AuditLogListItemDto>> GetListAsync([FromQuery] GetAuditLogListInput input)
        => _service.GetListAsync(input);

    /// <summary>按 ID 获取审计日志详情</summary>
    /// <param name="id">审计日志 ID</param>
    /// <returns>审计日志详情，包含实体变更和动作列表</returns>
    [HttpGet("{id}")]
    [Authorize(DredgeAIBasePermissions.AuditLogs.Default)]
    public Task<AuditLogDetailDto> GetAsync(Guid id)
        => _service.GetAsync(id);
}

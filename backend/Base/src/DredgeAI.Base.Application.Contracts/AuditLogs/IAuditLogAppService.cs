using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.AuditLogs;

/// <summary>审计日志管理应用服务接口（只读）</summary>
public interface IAuditLogAppService : IApplicationService
{
    /// <summary>分页查询审计日志列表</summary>
    Task<PagedResultDto<AuditLogListItemDto>> GetListAsync(GetAuditLogListInput input);

    /// <summary>按 ID 获取审计日志详情</summary>
    Task<AuditLogDetailDto> GetAsync(Guid id);
}

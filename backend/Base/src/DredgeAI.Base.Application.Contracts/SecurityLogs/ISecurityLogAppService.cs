using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.SecurityLogs;

/// <summary>安全日志管理应用服务接口（只读）</summary>
public interface ISecurityLogAppService : IApplicationService
{
    /// <summary>分页查询安全日志列表</summary>
    Task<PagedResultDto<SecurityLogListItemDto>> GetListAsync(GetSecurityLogListInput input);
}

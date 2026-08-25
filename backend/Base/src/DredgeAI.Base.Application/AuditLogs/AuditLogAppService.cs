using DredgeAI.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AuditLogging;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.AuditLogs;

/// <summary>审计日志管理应用服务（只读）</summary>
public class AuditLogAppService : DredgeAIBaseAppService, IAuditLogAppService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;

    public AuditLogAppService(
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository)
    {
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
    }

    public async Task<PagedResultDto<AuditLogListItemDto>> GetListAsync(GetAuditLogListInput input)
    {
        var totalCount = await _auditLogRepository.GetCountAsync(
            startTime: input.StartTime,
            endTime: input.EndTime,
            httpMethod: input.HttpMethod,
            url: input.Url,
            userId: input.UserId,
            userName: input.UserName,
            httpStatusCode: input.HttpStatusCode,
            hasException: input.HasException);

        var auditLogs = await _auditLogRepository.GetListAsync(
            sorting: input.Sorting,
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount,
            startTime: input.StartTime,
            endTime: input.EndTime,
            httpMethod: input.HttpMethod,
            url: input.Url,
            userId: input.UserId,
            userName: input.UserName,
            httpStatusCode: input.HttpStatusCode,
            hasException: input.HasException,
            includeDetails: false);

        var operatorMap = await BuildOperatorMapAsync(auditLogs);

        var items = auditLogs.Select(a =>
        {
            var dto = ObjectMapper.Map<AuditLog, AuditLogListItemDto>(a);
            dto.Operator = BuildOperatorInfo(a, operatorMap);
            return dto;
        }).ToList();

        return new PagedResultDto<AuditLogListItemDto>(totalCount, items);
    }

    public async Task<AuditLogDetailDto> GetAsync(Guid id)
    {
        var auditLog = await _auditLogRepository.GetAsync(id, includeDetails: true);

        var dto = ObjectMapper.Map<AuditLog, AuditLogDetailDto>(auditLog);
        dto.Operator = await BuildOperatorInfoAsync(auditLog);
        return dto;
    }

    private async Task<Dictionary<Guid, OperatorInfoDto>> BuildOperatorMapAsync(IEnumerable<AuditLog> auditLogs)
    {
        var userIds = auditLogs
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, OperatorInfoDto>();
        }

        var users = await _userRepository.GetListByUserIdsAsync(userIds);
        var roles = await _userRepository.GetRolesByUserIdsAsync(userIds);
        var orgUnits = await _userRepository.GetOrganizationUnitsByUserIdsAsync(userIds);

        return users.ToDictionary(
            u => u.Id,
            u => new OperatorInfoDto
            {
                UserId = u.Id,
                UserName = u.UserName,
                DisplayName = BuildDisplayName(u.Name, u.Surname),
                RoleNames = roles.GetValueOrDefault(u.Id, []),
                OrganizationUnits = orgUnits.GetValueOrDefault(u.Id, [])
                    .Select(ou => ou.DisplayName)
                    .OfType<string>()
                    .ToList()
            });
    }

    private async Task<OperatorInfoDto> BuildOperatorInfoAsync(AuditLog auditLog)
    {
        if (!auditLog.UserId.HasValue)
        {
            return new OperatorInfoDto();
        }

        var map = await BuildOperatorMapAsync(new[] { auditLog });
        if (map.TryGetValue(auditLog.UserId.Value, out var operatorInfo))
        {
            return operatorInfo;
        }

        return new OperatorInfoDto
        {
            UserId = auditLog.UserId,
            UserName = auditLog.UserName
        };
    }

    private static OperatorInfoDto BuildOperatorInfo(AuditLog auditLog, Dictionary<Guid, OperatorInfoDto> operatorMap)
    {
        if (!auditLog.UserId.HasValue)
        {
            return new OperatorInfoDto();
        }

        if (operatorMap.TryGetValue(auditLog.UserId.Value, out var operatorInfo))
        {
            return operatorInfo;
        }

        return new OperatorInfoDto
        {
            UserId = auditLog.UserId,
            UserName = auditLog.UserName
        };
    }

    private static string? BuildDisplayName(string? name, string? surname)
    {
        if (!string.IsNullOrWhiteSpace(surname))
        {
            return $"{name} {surname}".Trim();
        }

        return name;
    }
}

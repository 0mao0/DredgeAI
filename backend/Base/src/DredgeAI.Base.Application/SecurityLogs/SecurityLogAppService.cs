using System.Collections.Generic;
using System.Linq;
using DredgeAI.Permissions;
using DredgeAI.SecurityLogs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace DredgeAI.SecurityLogs;

/// <summary>安全日志管理应用服务（只读）</summary>
public class SecurityLogAppService : DredgeAIBaseAppService, ISecurityLogAppService
{
    private readonly IIdentitySecurityLogRepository _securityLogRepository;
    private readonly IUserRepository _userRepository;

    public SecurityLogAppService(
        IIdentitySecurityLogRepository securityLogRepository,
        IUserRepository userRepository)
    {
        _securityLogRepository = securityLogRepository;
        _userRepository = userRepository;
    }

    public async Task<PagedResultDto<SecurityLogListItemDto>> GetListAsync(GetSecurityLogListInput input)
    {
        var totalCount = await _securityLogRepository.GetCountAsync(
            startTime: input.StartTime,
            endTime: input.EndTime,
            identity: input.Identity,
            action: input.Action,
            userId: input.UserId,
            userName: input.UserName,
            clientIpAddress: input.ClientIpAddress);

        var securityLogs = await _securityLogRepository.GetListAsync(
            sorting: input.Sorting,
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount,
            startTime: input.StartTime,
            endTime: input.EndTime,
            identity: input.Identity,
            action: input.Action,
            userId: input.UserId,
            userName: input.UserName,
            clientIpAddress: input.ClientIpAddress,
            includeDetails: false);

        var userInfoMap = await BuildUserInfoMapAsync(securityLogs);

        var items = securityLogs.Select(s =>
        {
            var dto = ObjectMapper.Map<IdentitySecurityLog, SecurityLogListItemDto>(s);
            if (s.UserId.HasValue && userInfoMap.TryGetValue(s.UserId.Value, out var userInfo))
            {
                dto.DisplayName = userInfo.DisplayName;
                dto.RoleNames = userInfo.RoleNames;
                dto.OrganizationUnitNames = userInfo.OrganizationUnitNames;
            }
            return dto;
        }).ToList();

        return new PagedResultDto<SecurityLogListItemDto>(totalCount, items);
    }

    private async Task<Dictionary<Guid, UserInfo>> BuildUserInfoMapAsync(IEnumerable<IdentitySecurityLog> securityLogs)
    {
        var userIds = securityLogs
            .Where(s => s.UserId.HasValue)
            .Select(s => s.UserId!.Value)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserInfo>();
        }

        var users = await _userRepository.GetListByUserIdsAsync(userIds);
        var roles = await _userRepository.GetRolesByUserIdsAsync(userIds);
        var organizationUnits = await _userRepository.GetOrganizationUnitsByUserIdsAsync(userIds);

        return users.ToDictionary(
            u => u.Id,
            u => new UserInfo
            {
                DisplayName = BuildDisplayName(u.Name, u.Surname),
                RoleNames = roles.GetValueOrDefault(u.Id, []),
                OrganizationUnitNames = organizationUnits.GetValueOrDefault(u.Id, [])
                    .Select(ou => ou.DisplayName)
                    .ToList()
            });
    }

    private static string? BuildDisplayName(string? name, string? surname)
    {
        if (!string.IsNullOrWhiteSpace(surname))
        {
            return $"{name} {surname}".Trim();
        }

        return name;
    }

    private class UserInfo
    {
        public string? DisplayName { get; set; }
        public List<string> RoleNames { get; set; } = [];
        public List<string> OrganizationUnitNames { get; set; } = [];
    }
}

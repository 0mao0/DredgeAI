using Riok.Mapperly.Abstractions;
using DredgeAI.SecurityLogs;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SecurityLogToSecurityLogListItemDtoMapper : MapperBase<IdentitySecurityLog, SecurityLogListItemDto>
{
    [MapperIgnoreTarget(nameof(SecurityLogListItemDto.DisplayName))]
    [MapperIgnoreTarget(nameof(SecurityLogListItemDto.RoleNames))]
    [MapperIgnoreTarget(nameof(SecurityLogListItemDto.OrganizationUnitNames))]
    public override partial SecurityLogListItemDto Map(IdentitySecurityLog source);

    public override void Map(IdentitySecurityLog source, SecurityLogListItemDto destination)
    {
        throw new InvalidOperationException();
    }
}

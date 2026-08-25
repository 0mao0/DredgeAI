using Riok.Mapperly.Abstractions;
using DredgeAI.AuditLogs;
using Volo.Abp.AuditLogging;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AuditLogToAuditLogListItemDtoMapper : MapperBase<AuditLog, AuditLogListItemDto>
{
    [MapperIgnoreTarget(nameof(AuditLogListItemDto.Operator))]
    public override partial AuditLogListItemDto Map(AuditLog source);

    public override void Map(AuditLog source, AuditLogListItemDto destination)
    {
        throw new InvalidOperationException();
    }
}

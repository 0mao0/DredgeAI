using Riok.Mapperly.Abstractions;
using DredgeAI.AuditLogs;
using Volo.Abp.AuditLogging;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AuditLogToAuditLogDetailDtoMapper : MapperBase<AuditLog, AuditLogDetailDto>
{
    [MapperIgnoreTarget(nameof(AuditLogDetailDto.Operator))]
    public override partial AuditLogDetailDto Map(AuditLog source);

    public override void Map(AuditLog source, AuditLogDetailDto destination)
    {
        throw new InvalidOperationException();
    }
}

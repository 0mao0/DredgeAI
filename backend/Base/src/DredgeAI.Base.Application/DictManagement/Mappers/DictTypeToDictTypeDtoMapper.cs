using Riok.Mapperly.Abstractions;
using DredgeAI.DictManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DictTypeToDictTypeDtoMapper : MapperBase<DictType, DictTypeDto>
{
    [MapperIgnoreTarget(nameof(DictTypeDto.Children))]
    public override partial DictTypeDto Map(DictType source);

    public override void Map(DictType source, DictTypeDto destination)
    {
        throw new InvalidOperationException();
    }
}

using Riok.Mapperly.Abstractions;
using DredgeAI.DictManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DictTypeToDictTypeTreeNodeDtoMapper : MapperBase<DictType, DictTypeTreeNodeDto>
{
    [MapperIgnoreTarget(nameof(DictTypeTreeNodeDto.Children))]
    public override partial DictTypeTreeNodeDto Map(DictType source);

    public override void Map(DictType source, DictTypeTreeNodeDto destination)
    {
        throw new InvalidOperationException();
    }
}

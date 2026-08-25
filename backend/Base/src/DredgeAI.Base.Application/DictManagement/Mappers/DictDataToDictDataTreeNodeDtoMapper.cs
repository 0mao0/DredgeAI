using Riok.Mapperly.Abstractions;
using DredgeAI.DictManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DictDataToDictDataTreeNodeDtoMapper : MapperBase<DictData, DictDataTreeNodeDto>
{
    [MapperIgnoreTarget(nameof(DictDataTreeNodeDto.Children))]
    public override partial DictDataTreeNodeDto Map(DictData source);

    public override void Map(DictData source, DictDataTreeNodeDto destination)
    {
        throw new InvalidOperationException();
    }
}

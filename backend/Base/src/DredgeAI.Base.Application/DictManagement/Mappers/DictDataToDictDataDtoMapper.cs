using Riok.Mapperly.Abstractions;
using DredgeAI.DictManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[MapExtraProperties]
public partial class DictDataToDictDataDtoMapper : MapperBase<DictData, DictDataDto>
{
    [MapperIgnoreTarget(nameof(DictDataDto.Children))]
    public override partial DictDataDto Map(DictData source);

    public override void Map(DictData source, DictDataDto destination)
    {
        throw new InvalidOperationException();
    }
}

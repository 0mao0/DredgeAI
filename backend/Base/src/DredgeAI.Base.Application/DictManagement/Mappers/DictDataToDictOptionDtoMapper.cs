using Riok.Mapperly.Abstractions;
using DredgeAI.DictManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class DictDataToDictOptionDtoMapper : MapperBase<DictData, DictOptionDto>
{
    [MapProperty(nameof(DictData.Name), nameof(DictOptionDto.Title))]
    public override partial DictOptionDto Map(DictData source);

    public override void Map(DictData source, DictOptionDto destination)
    {
        throw new InvalidOperationException();
    }
}

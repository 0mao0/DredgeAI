using Riok.Mapperly.Abstractions;
using DredgeAI.MenuManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MenuInfoToMenuInfoDtoMapper : MapperBase<MenuInfo, MenuInfoDto>
{
    [MapperIgnoreTarget(nameof(MenuInfoDto.Children))]
    public override partial MenuInfoDto Map(MenuInfo source);

    public override void Map(MenuInfo source, MenuInfoDto destination)
    {
        throw new InvalidOperationException();
    }
}

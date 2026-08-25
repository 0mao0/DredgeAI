using Riok.Mapperly.Abstractions;
using DredgeAI.MenuManagement;
using Volo.Abp.Mapperly;

namespace DredgeAI;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MenuInfoToMenuTreeNodeDtoMapper : MapperBase<MenuInfo, MenuTreeNodeDto>
{
    [MapProperty(nameof(MenuInfo.Name), nameof(MenuTreeNodeDto.Name))]
    [MapProperty(nameof(MenuInfo.Title), nameof(MenuTreeNodeDto.Title))]
    [MapProperty(nameof(MenuInfo.Icon), nameof(MenuTreeNodeDto.Icon))]
    [MapProperty(nameof(MenuInfo.PermissionCode), nameof(MenuTreeNodeDto.PermissionCode))]
    [MapProperty(nameof(MenuInfo.RoutePath), nameof(MenuTreeNodeDto.Path))]
    [MapProperty(nameof(MenuInfo.ComponentPath), nameof(MenuTreeNodeDto.Component))]
    [MapProperty(nameof(MenuInfo.RedirectPath), nameof(MenuTreeNodeDto.Redirect))]
    [MapProperty(nameof(MenuInfo.Type), nameof(MenuTreeNodeDto.MenuType))]
    [MapProperty(nameof(MenuInfo.RoutePath), nameof(MenuTreeNodeDto.Url))]
    [MapProperty(nameof(MenuInfo.Id), nameof(MenuTreeNodeDto.Key))]
    [MapperIgnoreTarget(nameof(MenuTreeNodeDto.Children))]
    public override partial MenuTreeNodeDto Map(MenuInfo source);

    public override void Map(MenuInfo source, MenuTreeNodeDto destination)
    {
        destination.Key = source.Id.ToString();
        destination.Path = source.RoutePath ?? string.Empty;
        destination.Component = source.ComponentPath ?? string.Empty;
        destination.PermissionCode = source.PermissionCode;
        destination.Url = source.RoutePath ?? string.Empty;
        destination.Redirect = source.RedirectPath ?? string.Empty;
        destination.Title = source.Title;
        destination.MenuType = source.Type;
        destination.Name = source.Name;
        destination.Icon = source.Icon ?? string.Empty;
        destination.Children = new List<MenuTreeNodeDto>();
    }
}

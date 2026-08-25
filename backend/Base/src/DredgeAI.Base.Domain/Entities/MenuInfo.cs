using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI;

public class MenuInfo : FullAuditedAggregateRoot<Guid>
{
    public Guid? ParentId { get; private set; }
    public MenuType Type { get; private set; }
    public string Name { get; private set; }
    public string Title { get; private set; }
    public string? ComponentPath { get; private set; }
    public string? RoutePath { get; private set; }
    public string? RedirectPath { get; private set; }
    public string? Icon { get; private set; }
    public IconType IconType { get; private set; }
    public RouteType RouteType { get; private set; }
    public string PermissionCode { get; private set; }
    public uint SortId { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsCache { get; private set; }
    public bool IsFixed { get; private set; }
    public bool IsHidden { get; private set; }
    public bool IsStatic { get; private set; }
    public string? Remark { get; private set; }

    protected MenuInfo() { }

    internal MenuInfo(
        Guid id,
        Guid? parentId,
        MenuType type,
        string name,
        string title,
        string? componentPath,
        string? routePath,
        string? redirectPath,
        string? icon,
        IconType iconType,
        RouteType routeType,
        string permissionCode,
        uint sortId,
        bool isEnabled,
        bool isCache,
        bool isFixed,
        bool isHidden,
        bool isStatic,
        string? remark) : base(id)
    {
        ParentId = parentId;
        Type = type;
        SetName(name);
        SetTitle(title);
        ComponentPath = componentPath;
        RoutePath = routePath;
        RedirectPath = redirectPath;
        Icon = icon;
        IconType = iconType;
        RouteType = routeType;
        SetPermissionCode(permissionCode);
        SortId = sortId;
        IsEnabled = isEnabled;
        IsCache = isCache;
        IsFixed = isFixed;
        IsHidden = isHidden;
        IsStatic = isStatic;
        Remark = remark;
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MenuInfoConsts.MaxNameLength);
    }

    internal void SetTitle(string title)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), MenuInfoConsts.MaxTitleLength);
    }

    internal void SetPermissionCode(string permissionCode)
    {
        PermissionCode = Check.NotNullOrWhiteSpace(permissionCode, nameof(permissionCode), MenuInfoConsts.MaxPermissionCodeLength);
    }

    internal void SetParentId(Guid? parentId) => ParentId = parentId;
    internal void SetType(MenuType type) => Type = type;
    internal void SetComponentPath(string? componentPath) => ComponentPath = componentPath;
    internal void SetRoutePath(string? routePath) => RoutePath = routePath;
    internal void SetRedirectPath(string? redirectPath) => RedirectPath = redirectPath;
    internal void SetIcon(string? icon) => Icon = icon;
    internal void SetIconType(IconType iconType) => IconType = iconType;
    internal void SetRouteType(RouteType routeType) => RouteType = routeType;
    internal void SetSortId(uint sortId) => SortId = sortId;
    internal void SetIsEnabled(bool isEnabled) => IsEnabled = isEnabled;
    internal void SetIsCache(bool isCache) => IsCache = isCache;
    internal void SetIsFixed(bool isFixed) => IsFixed = isFixed;
    internal void SetIsHidden(bool isHidden) => IsHidden = isHidden;
    internal void SetIsStatic(bool isStatic) => IsStatic = isStatic;
    internal void SetRemark(string? remark) => Remark = remark;
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare.Applications;

public interface IApplicationCatalogAppService : IApplicationService
{
    Task<List<AppCatalogDto>> GetListAsync();

    Task<List<CategoryConfigDto>> GetCategoriesAsync();

    Task<List<AppPermissionTreeNodeDto>> GetPermissionTreeAsync();

    /// <summary>当前用户拥有 View 权限且已发布的应用目录（侧边栏动态应用菜单用）。</summary>
    Task<List<AppCatalogDto>> GetAuthorizedListAsync();

    Task<List<UserAppCardDto>> GetUserListAsync();

    Task SetAppStatusAsync(SetAppStatusInput input);

    Task SetSubStatusAsync(SetSubStatusInput input);

    Task SetAppCategoryAsync(SetAppFieldInput input);

    Task SetSubCategoryAsync(SetSubFieldInput input);

    Task SetAppIconAsync(SetAppIconInput input);

    Task SetSubIconAsync(SetSubIconInput input);

    Task<List<AppCatalogDto>> MoveAppAsync(MoveAppOrderInput input);

    Task<List<AppCatalogDto>> MoveSubAppAsync(MoveSubAppOrderInput input);
}

using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.MenuManagement;

/// <summary>菜单管理应用服务接口</summary>
/// <remarks>提供菜单的 CRUD 操作和树形查询能力</remarks>
public interface IMenuInfoAppService : IApplicationService
{
    /// <summary>按 ID 获取单个菜单</summary>
    /// <param name="id">菜单 ID</param>
    /// <returns>菜单详情</returns>
    Task<MenuInfoDto> GetAsync(Guid id);

    /// <summary>分页查询菜单列表</summary>
    /// <param name="input">查询条件，支持按名称关键词、类型、启用状态筛选</param>
    /// <returns>分页的菜单列表</returns>
    Task<PagedResultDto<MenuInfoDto>> GetListAsync(GetMenuInfoListInput input);

    /// <summary>创建菜单</summary>
    /// <param name="input">菜单创建参数</param>
    /// <returns>创建成功的菜单</returns>
    Task<MenuInfoDto> CreateAsync(MenuInfoCreateUpdateDto input);

    /// <summary>更新菜单</summary>
    /// <param name="id">菜单 ID</param>
    /// <param name="input">菜单更新参数</param>
    /// <returns>更新后的菜单</returns>
    Task<MenuInfoDto> UpdateAsync(Guid id, MenuInfoCreateUpdateDto input);

    /// <summary>删除菜单</summary>
    /// <param name="id">菜单 ID</param>
    Task DeleteAsync(Guid id);

    /// <summary>获取菜单树形结构</summary>
    /// <param name="input">查询条件，支持按类型和启用状态筛选</param>
    /// <returns>菜单树节点列表，Children 包含递归子菜单</returns>
    Task<List<MenuInfoDto>> GetTreeAsync(GetMenuInfoTreeInput input);

    /// <summary>获取当前用户拥有权限的菜单树</summary>
    /// <remarks>
    /// 与 GetTreeAsync 的区别：
    /// <list type="bullet">
    ///   <item>仅返回 <see cref="MenuType.Directory"/> 和 <see cref="MenuType.Menu"/> 类型菜单，排除 Button</item>
    ///   <item>仅返回 IsEnabled=true 且 IsHidden=false 的菜单</item>
    ///   <item><see cref="MenuInfoDto.PermissionCode"/> 为空时视为公共菜单，对所有已登录用户可见</item>
    ///   <item><see cref="MenuInfoDto.PermissionCode"/> 非空时，通过 ABP <c>IPermissionChecker.IsGrantedAsync</c> 校验当前用户权限，无权限的节点及其整个子树将被移除</item>
    /// </list>
    /// </remarks>
    /// <param name="input">查询条件，支持按名称关键词、类型和启用状态筛选（在权限过滤之前应用）</param>
    /// <returns>经过权限过滤后的菜单树，仅包含当前用户有权访问的菜单及公共菜单</returns>
    Task<List<MenuTreeNodeDto>> GetCurrentUserPermittedTreeAsync(GetMenuInfoTreeInput input);
}

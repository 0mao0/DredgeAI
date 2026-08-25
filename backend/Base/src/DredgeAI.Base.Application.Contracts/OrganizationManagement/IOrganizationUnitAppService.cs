using DredgeAI.Common;

using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.OrganizationManagement;

/// <summary>组织单位应用服务接口</summary>
public interface IOrganizationUnitAppService : IApplicationService
{
    /// <summary>获取组织单位树形结构</summary>
    Task<List<OrganizationUnitDto>> GetTreeAsync();
    /// <summary>获取 Ant Design Vue 树形结构</summary>
    Task<List<AndtTreeDto>> GetAndtTreeAsync();

    /// <summary>按 ID 获取单个组织单位</summary>
    Task<OrganizationUnitDto> GetAsync(Guid id);

    /// <summary>分页查询组织单位列表</summary>
    Task<PagedResultDto<OrganizationUnitDto>> GetListAsync(GetOrganizationUnitListInput input);

    /// <summary>创建组织单位</summary>
    Task<OrganizationUnitDto> CreateAsync(CreateOrganizationUnitDto input);

    /// <summary>更新组织单位</summary>
    Task<OrganizationUnitDto> UpdateAsync(Guid id, UpdateOrganizationUnitDto input);

    /// <summary>删除组织单位</summary>
    Task DeleteAsync(Guid id);

    /// <summary>层级关系校验（防止循环引用）</summary>
    Task HierarchyVerificationAsync(Guid id, Guid? parentId);
}

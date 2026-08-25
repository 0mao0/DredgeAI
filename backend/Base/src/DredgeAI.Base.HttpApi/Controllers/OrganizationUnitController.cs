using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.OrganizationManagement;
using DredgeAI.Common;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>组织单位管理接口</summary>
/// <remarks>组织单位支持多级树形结构，通过 ParentId 建立父子关系</remarks>
[Authorize]
[Route("api/base/organization-units")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("组织单位管理")]
public class OrganizationUnitController : DredgeAIBaseController, IOrganizationUnitAppService
{
    private readonly IOrganizationUnitAppService _service;

    public OrganizationUnitController(IOrganizationUnitAppService service)
    {
        _service = service;
    }

    /// <summary>获取组织单位树形结构</summary>
    /// <returns>组织单位树节点列表，根节点为 ParentId = null 的组织</returns>
    [HttpGet("tree")]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Default)]
    public Task<List<OrganizationUnitDto>> GetTreeAsync()
        => _service.GetTreeAsync();

    /// <summary>获取 Ant Design Vue 树形结构</summary>
    /// <returns>Ant Design Vue 树节点列表</returns>
    [HttpGet("andt-tree")]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Default)]
    public Task<List<AndtTreeDto>> GetAndtTreeAsync()
        => _service.GetAndtTreeAsync();

    /// <summary>按 ID 获取单个组织单位</summary>
    /// <param name="id">组织单位 ID</param>
    /// <returns>组织单位详情</returns>
    [HttpGet("{id}")]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Default)]
    public Task<OrganizationUnitDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>分页查询组织单位列表</summary>
    /// <param name="input">查询条件，支持按关键字搜索</param>
    /// <returns>分页的组织单位列表</returns>
    [HttpGet]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Default)]
    public Task<PagedResultDto<OrganizationUnitDto>> GetListAsync([FromQuery] GetOrganizationUnitListInput input)
        => _service.GetListAsync(input);

    /// <summary>创建组织单位</summary>
    /// <param name="input">组织单位创建参数</param>
    /// <returns>创建成功的组织单位</returns>
    [HttpPost]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Create)]
    public Task<OrganizationUnitDto> CreateAsync([FromBody] CreateOrganizationUnitDto input)
        => _service.CreateAsync(input);

    /// <summary>更新组织单位</summary>
    /// <param name="id">组织单位 ID</param>
    /// <param name="input">组织单位更新参数</param>
    /// <returns>更新后的组织单位</returns>
    [HttpPut("{id}")]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Update)]
    public Task<OrganizationUnitDto> UpdateAsync(Guid id, [FromBody] UpdateOrganizationUnitDto input)
        => _service.UpdateAsync(id, input);

    /// <summary>删除组织单位</summary>
    /// <param name="id">组织单位 ID</param>
    [HttpDelete("{id}")]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Delete)]
    public Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);

    /// <summary>层级关系校验</summary>
    /// <remarks>验证 parentId 是否为 id 的后代，防止形成循环引用</remarks>
    /// <param name="id">当前组织 ID</param>
    /// <param name="parentId">拟设置的上级组织 ID</param>
    [HttpGet("hierarchy-verification")]
    [Authorize(DredgeAIBasePermissions.OrganizationUnits.Update)]
    public Task HierarchyVerificationAsync(Guid id, Guid? parentId)
        => _service.HierarchyVerificationAsync(id, parentId);
}

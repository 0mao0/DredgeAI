using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.DictManagement;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>字典类型管理接口</summary>
/// <remarks>字典类型支持多级树形结构，通过 ParentId 建立父子关系</remarks>
[Authorize]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("字典管理")]
public class DictTypeController : DredgeAIBaseController, IDictTypeAppService
{
    private readonly IDictTypeAppService _service;

    public DictTypeController(IDictTypeAppService service)
    {
        _service = service;
    }

    /// <summary>分页查询字典类型列表</summary>
    /// <param name="input">查询条件，支持按关键字搜索和按父级 ID 筛选</param>
    /// <returns>分页的字典类型列表</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Default)]
    public Task<PagedResultDto<DictTypeDto>> GetListAsync([FromQuery] GetDictTypeListInput input)
        => _service.GetListAsync(input);

    /// <summary>按 ID 获取单个字典类型</summary>
    /// <param name="id">字典类型 ID</param>
    /// <returns>字典类型详情，包含子级列表</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Default)]
    public Task<DictTypeDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>创建字典类型</summary>
    /// <param name="input">字典类型创建参数，Code 留空将自动生成</param>
    /// <returns>创建成功的字典类型</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Create)]
    public Task<DictTypeDto> CreateAsync([FromBody] CreateDictTypeDto input)
        => _service.CreateAsync(input);

    /// <summary>更新字典类型</summary>
    /// <param name="id">字典类型 ID</param>
    /// <param name="input">字典类型更新参数</param>
    /// <returns>更新后的字典类型</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Update)]
    public Task<DictTypeDto> UpdateAsync(Guid id, [FromBody] UpdateDictTypeDto input)
        => _service.UpdateAsync(id, input);

    /// <summary>删除字典类型</summary>
    /// <param name="id">字典类型 ID</param>
    /// <param name="cascade">是否级联删除所有子级字典类型和字典数据，默认 false</param>
    [Authorize(DredgeAIBasePermissions.DictTypes.Delete)]
    public Task DeleteAsync(Guid id, [FromQuery] bool cascade = false)
        => _service.DeleteAsync(id, cascade);

    /// <summary>获取全部字典类型的树形结构</summary>
    /// <returns>字典类型树节点列表，根节点为 ParentId = null 的类型</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Default)]
    public Task<List<DictTypeTreeNodeDto>> GetTreeAsync()
        => _service.GetTreeAsync();

    /// <summary>获取指定父级下的子字典类型列表</summary>
    /// <param name="parentId">父级字典类型 ID</param>
    /// <returns>直接子级字典类型列表（不含后代层级）</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Default)]
    public Task<List<DictTypeDto>> GetChildrenAsync(Guid parentId)
        => _service.GetChildrenAsync(parentId);

    /// <summary>根据模块编码自动生成字典类型编码</summary>
    /// <param name="moduleCode">模块编码，可选。如 "SYS_USER" 表示用户模块</param>
    /// <returns>自动生成的完整编码字符串</returns>
    [Authorize(DredgeAIBasePermissions.DictTypes.Create)]
    public Task<string> GenerateCodeAsync([FromQuery] string? moduleCode)
        => _service.GenerateCodeAsync(moduleCode);
}

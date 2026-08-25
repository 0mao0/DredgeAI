using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.DictManagement;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Controllers;

/// <summary>字典数据管理接口</summary>
/// <remarks>字典数据挂载在字典类型下，支持多级树形结构和选项查询</remarks>
[Authorize]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("字典管理")]
public class DictDataController : DredgeAIBaseController, IDictDataAppService
{
    private readonly IDictDataAppService _service;

    public DictDataController(IDictDataAppService service)
    {
        _service = service;
    }

    /// <summary>分页查询字典数据列表</summary>
    /// <param name="input">查询条件，必须指定字典类型编码（TypeCode），支持按关键字搜索和按父级 ID 筛选</param>
    /// <returns>分页的字典数据列表</returns>
    [Authorize(DredgeAIBasePermissions.DictData.Default)]
    public Task<PagedResultDto<DictDataDto>> GetListAsync([FromQuery] GetDictDataListInput input)
        => _service.GetListAsync(input);

    /// <summary>按 ID 获取单个字典数据</summary>
    /// <param name="id">字典数据 ID</param>
    /// <returns>字典数据详情</returns>
    [Authorize(DredgeAIBasePermissions.DictData.Default)]
    public Task<DictDataDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>创建字典数据</summary>
    /// <param name="input">字典数据创建参数</param>
    /// <returns>创建成功的字典数据</returns>
    [Authorize(DredgeAIBasePermissions.DictData.Create)]
    public Task<DictDataDto> CreateAsync([FromBody] CreateDictDataDto input)
        => _service.CreateAsync(input);

    /// <summary>更新字典数据</summary>
    /// <param name="id">字典数据 ID</param>
    /// <param name="input">字典数据更新参数</param>
    /// <returns>更新后的字典数据</returns>
    [Authorize(DredgeAIBasePermissions.DictData.Update)]
    public Task<DictDataDto> UpdateAsync(Guid id, [FromBody] UpdateDictDataDto input)
        => _service.UpdateAsync(id, input);

    /// <summary>删除字典数据</summary>
    /// <param name="id">字典数据 ID</param>
    [Authorize(DredgeAIBasePermissions.DictData.Delete)]
    public Task DeleteAsync(Guid id)
        => _service.DeleteAsync(id);

    /// <summary>获取字典数据的下拉选项列表</summary>
    /// <param name="input">查询条件，必须指定字典类型编码（TypeCode），可选排除指定值</param>
    /// <returns>用于前端下拉框的选项列表</returns>
    [Authorize(DredgeAIBasePermissions.DictData.Default)]
    public Task<List<DictOptionDto>> GetOptionsAsync([FromQuery] DictDataOptionInput input)
        => _service.GetOptionsAsync(input);

    /// <summary>按字典类型获取字典数据的树形结构</summary>
    /// <param name="typeId">字典类型 ID</param>
    /// <returns>该字典类型下所有字典数据的树形结构</returns>
    [Authorize(DredgeAIBasePermissions.DictData.Default)]
    public Task<List<DictDataTreeNodeDto>> GetTreeAsync(Guid typeId)
        => _service.GetTreeAsync(typeId);
}

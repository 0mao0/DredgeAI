using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.DictManagement;

/// <summary>字典类型应用服务接口</summary>
public interface IDictTypeAppService : IApplicationService
{
    /// <summary>按 ID 获取单个字典类型</summary>
    Task<DictTypeDto> GetAsync(Guid id);

    /// <summary>分页查询字典类型列表</summary>
    Task<PagedResultDto<DictTypeDto>> GetListAsync(GetDictTypeListInput input);

    /// <summary>创建字典类型</summary>
    Task<DictTypeDto> CreateAsync(CreateDictTypeDto input);

    /// <summary>更新字典类型</summary>
    Task<DictTypeDto> UpdateAsync(Guid id, UpdateDictTypeDto input);

    /// <summary>删除字典类型</summary>
    Task DeleteAsync(Guid id, bool cascade);

    /// <summary>获取全部字典类型的树形结构</summary>
    Task<List<DictTypeTreeNodeDto>> GetTreeAsync();

    /// <summary>获取指定父级下的子字典类型列表</summary>
    Task<List<DictTypeDto>> GetChildrenAsync(Guid parentId);

    /// <summary>根据模块编码自动生成字典类型编码</summary>
    Task<string> GenerateCodeAsync(string? moduleCode);
}

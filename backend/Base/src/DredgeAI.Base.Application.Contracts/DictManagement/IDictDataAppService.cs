using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.DictManagement;

/// <summary>字典数据应用服务接口</summary>
public interface IDictDataAppService : IApplicationService
{
    /// <summary>按 ID 获取单个字典数据</summary>
    Task<DictDataDto> GetAsync(Guid id);

    /// <summary>分页查询字典数据列表</summary>
    Task<PagedResultDto<DictDataDto>> GetListAsync(GetDictDataListInput input);

    /// <summary>创建字典数据</summary>
    Task<DictDataDto> CreateAsync(CreateDictDataDto input);

    /// <summary>更新字典数据</summary>
    Task<DictDataDto> UpdateAsync(Guid id, UpdateDictDataDto input);

    /// <summary>删除字典数据</summary>
    Task DeleteAsync(Guid id);

    /// <summary>获取字典数据的下拉选项列表</summary>
    Task<List<DictOptionDto>> GetOptionsAsync(DictDataOptionInput input);

    /// <summary>按字典类型获取字典数据的树形结构</summary>
    Task<List<DictDataTreeNodeDto>> GetTreeAsync(Guid typeId);
}

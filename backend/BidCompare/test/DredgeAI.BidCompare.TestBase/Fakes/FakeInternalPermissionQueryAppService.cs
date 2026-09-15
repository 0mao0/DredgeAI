using System.Collections.Generic;
using System.Threading.Tasks;
using DredgeAI.Permissions;

namespace DredgeAI.BidCompare.Applications;

/// <summary>Fake 内部权限查询：授权条目由测试直接赋值。</summary>
public class FakeInternalPermissionQueryAppService : IInternalPermissionQueryAppService
{
    public List<ResourcePermissionGrantItemDto> Grants { get; set; } = [];

    public Task<List<ResourcePermissionGrantItemDto>> GetResourceGrantsAsync(string resourceName, string permissionName)
        => Task.FromResult(Grants);
}

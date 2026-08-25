using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.Permissions;
using Volo.Abp;

namespace DredgeAI.Controllers;

/// <summary>权限定义查询接口</summary>
/// <remarks>提供系统所有权限定义的树形结构查询。</remarks>
[Authorize]
[Route("api/base/permission-definitions")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("权限管理")]
public class PermissionDefinitionController : DredgeAIBaseController, IPermissionDefinitionAppService
{
    private readonly IPermissionDefinitionAppService _service;

    public PermissionDefinitionController(IPermissionDefinitionAppService service)
    {
        _service = service;
    }

    /// <summary>获取权限定义树</summary>
    /// <param name="providerName">
    /// 授权提供者名称。R = 角色，U = 用户。传入时返回带 isGranted 字段的授权树。
    /// </param>
    /// <param name="providerKey">
    /// 授权提供者键（角色名或用户 GUID）。仅当 providerName 同时传入时生效。
    /// </param>
    /// <returns>权限组及其权限的树形结构</returns>
    [HttpGet("permission-tree")]
    [Authorize]
    public Task<List<PermissionGroupTreeDto>> GetTreeAsync(string? providerName, string? providerKey)
        => _service.GetTreeAsync(providerName, providerKey);
}

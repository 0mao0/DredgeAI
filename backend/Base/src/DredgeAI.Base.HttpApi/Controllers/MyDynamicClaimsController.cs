using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.Controllers;

/// <summary>
/// 动态声明控制器，替换 ABP 内置的 <see cref="DynamicClaimsController"/>，
/// 用于刷新当前用户的动态 Claims（如权限变更后即时生效）。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(DynamicClaimsController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/account/dynamic-claims")]
[Tags("账号管理")]
public class MyDynamicClaimsController : DynamicClaimsController
{
    public MyDynamicClaimsController(IDynamicClaimsAppService dynamicClaimsAppService) : base(dynamicClaimsAppService)
    {
    }

    /// <summary>
    /// 刷新当前登录用户的动态 Claims，使权限变更即时生效而无需重新登录。
    /// </summary>
    [HttpPost]
    [Route("refresh")]
    public override Task RefreshAsync()
    {
        return DynamicClaimsAppService.RefreshAsync();
    }
}

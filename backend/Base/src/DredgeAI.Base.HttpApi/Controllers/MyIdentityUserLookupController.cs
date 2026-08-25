using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace DredgeAI.Controllers;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityUserLookupController),IncludeSelf = true)]
[Tags("用户管理")]
[RemoteService(false)]
public class MyIdentityUserLookupController:IdentityUserLookupController
{
    public MyIdentityUserLookupController(IIdentityUserLookupAppService lookupAppService) : base(lookupAppService)
    {
    }


    /// <summary>
    /// 根据用户ID查找用户信息。
    /// </summary>
    /// <param name="id">用户的唯一标识符 (GUID)。</param>
    /// <returns>返回包含用户信息的 <see cref="UserData"/> 对象。</returns>
    [HttpGet]
    [Route("{id}")]
    public  override  Task<UserData> FindByIdAsync(Guid id)
    {
        return LookupAppService.FindByIdAsync(id);
    }

    /// <summary>
    /// 根据用户名查找用户信息。
    /// </summary>
    /// <param name="userName">需要查找的用户名。</param>
    /// <return>返回与指定用户名匹配的用户数据。</return>
    [HttpGet]
    [Route("by-username/{userName}")]
    public  override  Task<UserData> FindByUserNameAsync(string userName)
    {
        return LookupAppService.FindByUserNameAsync(userName);
    }
}
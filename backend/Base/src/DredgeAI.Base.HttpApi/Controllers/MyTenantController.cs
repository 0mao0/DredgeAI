using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.TenantManagement;

namespace DredgeAI.Controllers;

/// <summary>
/// 租户管理控制器，替换 ABP 内置的 <see cref="TenantController"/>，
/// 使用 Base 模块自定义权限校验租户 CRUD 及连接字符串管理操作。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(TenantController), IncludeSelf = true)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/multi-tenancy/tenants")]
[Tags("租户管理")]
public class MyTenantController : TenantController
{
    public MyTenantController(ITenantAppService tenantAppService) : base(tenantAppService)
    {
    }

    /// <summary>
    /// 根据租户ID获取单个租户详情。
    /// </summary>
    /// <param name="id">租户的唯一标识符 (GUID)。</param>
    /// <returns>包含租户信息的 <see cref="TenantDto"/> 对象。</returns>
    [HttpGet]
    [Route("{id}")]
    public override Task<TenantDto> GetAsync(Guid id)
    {
        return TenantAppService.GetAsync(id);
    }

    /// <summary>
    /// 获取分页的租户列表，支持按租户名模糊筛选。
    /// </summary>
    /// <param name="input">包含分页、排序和筛选条件的 <see cref="GetTenantsInput"/> 对象。</param>
    /// <returns>分页的租户数据列表。</returns>
    [HttpGet]
    public override Task<PagedResultDto<TenantDto>> GetListAsync(GetTenantsInput input)
    {
        return TenantAppService.GetListAsync(input);
    }

    /// <summary>
    /// 创建新租户，自动以新租户身份执行种子数据初始化（含管理员账户），
    /// 并通过分布式事件通知其他服务。
    /// </summary>
    /// <param name="input">包含租户名和管理员邮箱、密码的 <see cref="TenantCreateDto"/> 对象。</param>
    /// <returns>创建成功的租户信息。</returns>
    [HttpPost]
    public override Task<TenantDto> CreateAsync(TenantCreateDto input)
    {
        return TenantAppService.CreateAsync(input);
    }

    /// <summary>
    /// 更新指定租户的名称。
    /// </summary>
    /// <param name="id">要更新的租户ID。</param>
    /// <param name="input">包含新租户名的 <see cref="TenantUpdateDto"/> 对象。</param>
    /// <returns>更新后的租户信息。</returns>
    [HttpPut]
    [Route("{id}")]
    public override Task<TenantDto> UpdateAsync(Guid id, TenantUpdateDto input)
    {
        return TenantAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// 删除指定租户。
    /// </summary>
    /// <param name="id">要删除的租户ID。</param>
    [HttpDelete]
    [Route("{id}")]
    public override Task DeleteAsync(Guid id)
    {
        return TenantAppService.DeleteAsync(id);
    }

    /// <summary>
    /// 获取指定租户的默认数据库连接字符串。
    /// </summary>
    /// <param name="id">租户ID。</param>
    /// <returns>默认连接字符串，未设置时返回 null。</returns>
    [HttpGet]
    [Route("{id}/default-connection-string")]
    public override Task<string> GetDefaultConnectionStringAsync(Guid id)
    {
        return TenantAppService.GetDefaultConnectionStringAsync(id);
    }

    /// <summary>
    /// 更新指定租户的默认数据库连接字符串，变更时发布本地事件通知。
    /// </summary>
    /// <param name="id">租户ID。</param>
    /// <param name="defaultConnectionString">新的默认连接字符串。</param>
    [HttpPut]
    [Route("{id}/default-connection-string")]
    public override Task UpdateDefaultConnectionStringAsync(Guid id, string defaultConnectionString)
    {
        return TenantAppService.UpdateDefaultConnectionStringAsync(id, defaultConnectionString);
    }

    /// <summary>
    /// 删除指定租户的默认数据库连接字符串，恢复为全局默认连接。
    /// </summary>
    /// <param name="id">租户ID。</param>
    [HttpDelete]
    [Route("{id}/default-connection-string")]
    public override Task DeleteDefaultConnectionStringAsync(Guid id)
    {
        return TenantAppService.DeleteDefaultConnectionStringAsync(id);
    }
}

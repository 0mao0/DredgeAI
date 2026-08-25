using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.DependencyInjection;
using Volo.Abp.FeatureManagement;

namespace DredgeAI.Controllers;

/// <summary>
/// 特性管理控制器，替换 ABP 内置的 <see cref="FeaturesController"/>，
/// 使用 Base 模块自定义权限校验 Host 特性管理操作。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(FeaturesController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/feature-management/features")]
[Tags("特性管理")]
public class MyFeaturesController : FeaturesController
{
    public MyFeaturesController(IFeatureAppService featureAppService) : base(featureAppService)
    {
    }

    /// <summary>
    /// 获取指定 Provider 下的所有特性及其当前值，按分组层级展示。
    /// 当 Provider 为租户 (T) 且无租户上下文时，校验 Host 特性管理权限。
    /// </summary>
    /// <param name="providerName">特性值提供者名称（如 "T" 表示租户）。</param>
    /// <param name="providerKey">特性值提供者 Key（如租户ID）。</param>
    /// <returns>分组层级结构的特性列表及其当前值。</returns>
    [HttpGet]
    public override Task<GetFeatureListResultDto> GetAsync(string providerName, string providerKey)
    {
        return FeatureAppService.GetAsync(providerName, providerKey);
    }

    /// <summary>
    /// 批量更新指定 Provider 下的特性值。父子特性自动联动，
    /// 当子特性值被显式设置到当前 Provider 时，父特性值也会被强制写入。
    /// </summary>
    /// <param name="providerName">特性值提供者名称。</param>
    /// <param name="providerKey">特性值提供者 Key。</param>
    /// <param name="input">包含待更新特性名值对的 DTO。</param>
    [HttpPut]
    public override Task UpdateAsync(string providerName, string providerKey, UpdateFeaturesDto input)
    {
        return FeatureAppService.UpdateAsync(providerName, providerKey, input);
    }

    /// <summary>
    /// 删除指定 Provider 下的所有特性值，恢复为默认值。
    /// </summary>
    /// <param name="providerName">特性值提供者名称。</param>
    /// <param name="providerKey">特性值提供者 Key。</param>
    [HttpDelete]
    public override Task DeleteAsync(string providerName, string providerKey)
    {
        return FeatureAppService.DeleteAsync(providerName, providerKey);
    }
}

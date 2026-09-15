namespace DredgeAI.Permissions;

/// <summary>资源授权条目（集成服务出参）：Provider 三元组 + 资源 Key。</summary>
public class ResourcePermissionGrantItemDto
{
    public string ProviderName { get; set; } = default!;

    public string ProviderKey { get; set; } = default!;

    public string ResourceKey { get; set; } = default!;
}

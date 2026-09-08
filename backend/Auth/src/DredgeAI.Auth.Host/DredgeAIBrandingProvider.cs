using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace DredgeAI;

/// <summary>
/// 统一认证中心品牌：替换默认 "MyApplication"，影响页面 &lt;title&gt; 等所有引用 IBrandingProvider 的位置。
/// </summary>
[Dependency(ReplaceServices = true)]
public class DredgeAIBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "智浚 AI";
}

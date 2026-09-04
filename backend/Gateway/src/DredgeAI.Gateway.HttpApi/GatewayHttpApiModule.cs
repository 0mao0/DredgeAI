using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class GatewayHttpApiModule : AbpModule
{
}

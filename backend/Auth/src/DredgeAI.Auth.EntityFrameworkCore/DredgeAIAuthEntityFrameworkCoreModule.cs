using Shiw.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace DredgeAI;


[DependsOn(
    typeof(DredgeAIAuthDomainModule),
    typeof(AbpEntityFrameworkCoreModule),
    typeof(ShiwIdentityEntityFrameworkCoreModule)
)]
public class DredgeAIAuthEntityFrameworkCoreModule:AbpModule
{

}
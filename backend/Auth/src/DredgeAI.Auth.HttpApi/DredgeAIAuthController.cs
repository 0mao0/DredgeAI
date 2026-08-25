using DredgeAI.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI;

public abstract class DredgeAIAuthController : AbpControllerBase
{
    protected DredgeAIAuthController()
    {
        LocalizationResource = typeof(DredgeAIAuthResource);
    }
}
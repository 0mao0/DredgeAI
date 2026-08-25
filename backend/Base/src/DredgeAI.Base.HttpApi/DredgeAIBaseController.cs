using DredgeAI.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI;

public abstract class DredgeAIBaseController : AbpControllerBase
{
    protected DredgeAIBaseController()
    {
        LocalizationResource = typeof(DredgeAIBaseResource);
    }
}
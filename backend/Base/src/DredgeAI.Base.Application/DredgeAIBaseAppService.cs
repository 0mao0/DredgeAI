using DredgeAI.Localization;
using Volo.Abp.Application.Services;

namespace DredgeAI;

public abstract class DredgeAIBaseAppService : ApplicationService
{
    protected DredgeAIBaseAppService()
    {
        LocalizationResource = typeof(DredgeAIBaseResource);
        ObjectMapperContext = typeof(DredgeAIBaseApplicationModule);
    }
}
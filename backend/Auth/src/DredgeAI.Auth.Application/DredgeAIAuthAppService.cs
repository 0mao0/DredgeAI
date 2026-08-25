using DredgeAI.Localization;
using Volo.Abp.Application.Services;

namespace DredgeAI;

public abstract class DredgeAIAuthAppService : ApplicationService
{
    protected DredgeAIAuthAppService()
    {
        LocalizationResource = typeof(DredgeAIAuthResource);
        ObjectMapperContext = typeof(DredgeAIAuthApplicationModule);
    }
}
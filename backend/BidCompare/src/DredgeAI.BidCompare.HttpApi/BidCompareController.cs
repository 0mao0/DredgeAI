using DredgeAI.BidCompare.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

public abstract class BidCompareController : AbpControllerBase
{
    protected BidCompareController()
    {
        LocalizationResource = typeof(BidCompareResource);
    }
}

using DredgeAI.BidCompare.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class BidCompareController : AbpControllerBase
{
    protected BidCompareController()
    {
        LocalizationResource = typeof(BidCompareResource);
    }
}

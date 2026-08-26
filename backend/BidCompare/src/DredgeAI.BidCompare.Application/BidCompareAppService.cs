using System;
using System.Collections.Generic;
using System.Text;
using DredgeAI.BidCompare.Localization;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare;

/* Inherit your application services from this class.
 */
public abstract class BidCompareAppService : ApplicationService
{
    protected BidCompareAppService()
    {
        LocalizationResource = typeof(BidCompareResource);
    }
}

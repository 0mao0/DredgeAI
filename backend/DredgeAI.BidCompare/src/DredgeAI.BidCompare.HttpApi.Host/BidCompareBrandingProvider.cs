using Microsoft.Extensions.Localization;
using DredgeAI.BidCompare.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace DredgeAI.BidCompare;

[Dependency(ReplaceServices = true)]
public class BidCompareBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<BidCompareResource> _localizer;

    public BidCompareBrandingProvider(IStringLocalizer<BidCompareResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}

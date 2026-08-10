using Volo.Abp.Settings;

namespace DredgeAI.BidCompare.Settings;

public class BidCompareSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(BidCompareSettings.MySetting1));
    }
}

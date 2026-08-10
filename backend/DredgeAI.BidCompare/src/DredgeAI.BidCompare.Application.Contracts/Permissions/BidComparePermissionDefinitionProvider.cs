using DredgeAI.BidCompare.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace DredgeAI.BidCompare.Permissions;

public class BidComparePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(BidComparePermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(BidComparePermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BidCompareResource>(name);
    }
}

using DredgeAI.BidCompare.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace DredgeAI.BidCompare.Permissions;

public class BidComparePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(BidComparePermissions.GroupName, L("Permission:BidCompare"));

        var aiMeeting = myGroup.AddPermission(BidComparePermissions.AiMeeting.Default, L("Permission:BidCompare.AiMeeting"));
        aiMeeting.AddChild(BidComparePermissions.AiMeeting.ManageProjects, L("Permission:BidCompare.AiMeeting.ManageProjects"));
        aiMeeting.AddChild(BidComparePermissions.AiMeeting.ManageWorkers, L("Permission:BidCompare.AiMeeting.ManageWorkers"));

        var appCatalog = myGroup.AddPermission(BidComparePermissions.AppCatalog.Default, L("Permission:BidCompare.AppCatalog"));
        appCatalog.AddChild(BidComparePermissions.AppCatalog.Manage, L("Permission:BidCompare.AppCatalog.Manage"));
        appCatalog.AddChild(BidComparePermissions.AppCatalog.ManagePermissions, L("Permission:BidCompare.AppCatalog.ManagePermissions"));
        
        _ = context.AddResourcePermission(
            name: BidComparePermissions.AppCatalog.Resources.View,
            resourceName: BidComparePermissions.AppCatalog.Resources.Name,
            managementPermissionName: BidComparePermissions.AppCatalog.ManagePermissions,
            displayName: L("Permission:BidCompare.AppCatalog.Resources.View"));

        _ = context.AddResourcePermission(
            name: BidComparePermissions.AppCatalog.Resources.Edit,
            resourceName: BidComparePermissions.AppCatalog.Resources.Name,
            managementPermissionName: BidComparePermissions.AppCatalog.ManagePermissions,
            displayName: L("Permission:BidCompare.AppCatalog.Resources.Edit"));

        _ = context.AddResourcePermission(
            name: BidComparePermissions.AppCatalog.Resources.Delete,
            resourceName: BidComparePermissions.AppCatalog.Resources.Name,
            managementPermissionName: BidComparePermissions.AppCatalog.ManagePermissions,
            displayName: L("Permission:BidCompare.AppCatalog.Resources.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BidCompareResource>(name);
    }
}

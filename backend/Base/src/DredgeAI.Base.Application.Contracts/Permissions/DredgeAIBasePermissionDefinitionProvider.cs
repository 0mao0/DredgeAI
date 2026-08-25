using DredgeAI.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace DredgeAI.Permissions;

public class DredgeAIBasePermissionDefinitionProvider:PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(DredgeAIBasePermissions.GroupName, L("Permission:Base"));

        _ = myGroup.AddPermission(DredgeAIBasePermissions.AuditLogs.Default, L("Permission:AuditLogs"));

        _ = myGroup.AddPermission(DredgeAIBasePermissions.SecurityLogs.Default, L("Permission:SecurityLogs"));

        var dictTypes = myGroup.AddPermission(DredgeAIBasePermissions.DictTypes.Default, L("Permission:DictTypes"));
        dictTypes.AddChild(DredgeAIBasePermissions.DictTypes.Create, L("Permission:DictTypes.Create"));
        dictTypes.AddChild(DredgeAIBasePermissions.DictTypes.Update, L("Permission:DictTypes.Update"));
        dictTypes.AddChild(DredgeAIBasePermissions.DictTypes.Delete, L("Permission:DictTypes.Delete"));

        var dictData = myGroup.AddPermission(DredgeAIBasePermissions.DictData.Default, L("Permission:DictData"));
        dictData.AddChild(DredgeAIBasePermissions.DictData.Create, L("Permission:DictData.Create"));
        dictData.AddChild(DredgeAIBasePermissions.DictData.Update, L("Permission:DictData.Update"));
        dictData.AddChild(DredgeAIBasePermissions.DictData.Delete, L("Permission:DictData.Delete"));

        var menus = myGroup.AddPermission(DredgeAIBasePermissions.Menus.Default, L("Permission:Menus"));
        menus.AddChild(DredgeAIBasePermissions.Menus.Create, L("Permission:Menus.Create"));
        menus.AddChild(DredgeAIBasePermissions.Menus.Update, L("Permission:Menus.Update"));
        menus.AddChild(DredgeAIBasePermissions.Menus.Delete, L("Permission:Menus.Delete"));

        var orgUnits = myGroup.AddPermission(DredgeAIBasePermissions.OrganizationUnits.Default, L("Permission:OrganizationUnits"));
        orgUnits.AddChild(DredgeAIBasePermissions.OrganizationUnits.Create, L("Permission:OrganizationUnits.Create"));
        orgUnits.AddChild(DredgeAIBasePermissions.OrganizationUnits.Update, L("Permission:OrganizationUnits.Update"));
        orgUnits.AddChild(DredgeAIBasePermissions.OrganizationUnits.Delete, L("Permission:OrganizationUnits.Delete"));

        var users = myGroup.AddPermission(DredgeAIBasePermissions.Users.Default, L("Permission:Users"));
        users.AddChild(DredgeAIBasePermissions.Users.Create, L("Permission:Users.Create"));
        users.AddChild(DredgeAIBasePermissions.Users.Update, L("Permission:Users.Update"));
        users.AddChild(DredgeAIBasePermissions.Users.Delete, L("Permission:Users.Delete"));
        users.AddChild(DredgeAIBasePermissions.Users.ManagePermissions, L("Permission:Users.ManagePermissions"));
        users.AddChild(DredgeAIBasePermissions.Users.ManageRoles, L("Permission:Users.ManageRoles"));

        var roles = myGroup.AddPermission(DredgeAIBasePermissions.Roles.Default, L("Permission:Roles"));
        roles.AddChild(DredgeAIBasePermissions.Roles.Create, L("Permission:Roles.Create"));
        roles.AddChild(DredgeAIBasePermissions.Roles.Update, L("Permission:Roles.Update"));
        roles.AddChild(DredgeAIBasePermissions.Roles.Delete, L("Permission:Roles.Delete"));
        roles.AddChild(DredgeAIBasePermissions.Roles.ManagePermissions, L("Permission:Roles.ManagePermissions"));

        var features = myGroup.AddPermission(DredgeAIBasePermissions.Features.Default, L("Permission:Features"));
        features.AddChild(DredgeAIBasePermissions.Features.ManageHostFeatures, L("Permission:Features.ManageHostFeatures"));

        var tenants = myGroup.AddPermission(DredgeAIBasePermissions.Tenants.Default, L("Permission:Tenants"));
        tenants.AddChild(DredgeAIBasePermissions.Tenants.Create, L("Permission:Tenants.Create"));
        tenants.AddChild(DredgeAIBasePermissions.Tenants.Update, L("Permission:Tenants.Update"));
        tenants.AddChild(DredgeAIBasePermissions.Tenants.Delete, L("Permission:Tenants.Delete"));
        tenants.AddChild(DredgeAIBasePermissions.Tenants.ManageFeatures, L("Permission:Tenants.ManageFeatures"));
        tenants.AddChild(DredgeAIBasePermissions.Tenants.ManageConnectionStrings, L("Permission:Tenants.ManageConnectionStrings"));
    }
    
    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DredgeAIBaseResource>(name);
    }
    
}
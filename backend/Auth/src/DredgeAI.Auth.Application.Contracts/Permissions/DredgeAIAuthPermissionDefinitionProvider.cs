using DredgeAI.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace DredgeAI.Permissions;

public class DredgeAIAuthPermissionDefinitionProvider:PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(DredgeAIAuthPermissions.GroupName, L("Permission:Auth"));
    }
    
    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DredgeAIAuthResource>(name);
    }
    
}
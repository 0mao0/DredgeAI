using Volo.Abp.Reflection;

namespace DredgeAI.Permissions;

public class DredgeAIAuthPermissions
{
    public const string GroupName = "Auth";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(DredgeAIAuthPermissions));
    }
}
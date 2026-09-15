using Volo.Abp.Reflection;

namespace DredgeAI.BidCompare.Permissions;

public class BidComparePermissions
{
    public const string GroupName = "BidCompare";

    public static class AiMeeting
    {
        public const string Default = GroupName + ".AiMeeting";
        public const string ManageProjects = Default + ".ManageProjects"; // 施工项目管理
        public const string ManageWorkers = Default + ".ManageWorkers";   // 工人档案与人脸库
    }

    public static class AppCatalog
    {
        public const string Default = GroupName + ".AppCatalog";
        public const string Manage = Default + ".Manage";                 // 发布管理（上下架/分类/图标/排序）
        public const string ManagePermissions = Default + ".ManagePermissions"; // 资源权限的管理入口（AddResourcePermission 必需）

        public static class Resources
        {
            public const string Name = "DredgeAI.BidCompare.Applications.AppCatalog"; // AppCatalog 实体全名
            public const string View = Name + ".View";
            public const string Edit = Name + ".Edit";
            public const string Delete = Name + ".Delete";
        }
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(BidComparePermissions));
    }
}

using System;
using Volo.Abp.Account;
using Volo.Abp.ObjectExtending;

namespace DredgeAI.Account;

public static class ProfileObjectExtensions
{
    public static void Configure()
    {
        // 四个属性均为只读输出（仅 ProfileDto），由应用服务计算填充；
        // DepartmentNames 来自组织机构，不落 IdentityUser.ExtraProperties，不进 UpdateProfileDto
        ObjectExtensionManager.Instance.AddOrUpdate<ProfileDto>(options =>
        {
            options.AddOrUpdateProperty<string[]>(UserExtensionConsts.DepartmentNamesPropertyName);
            options.AddOrUpdateProperty<string[]>(UserExtensionConsts.RoleNamesPropertyName);
            options.AddOrUpdateProperty<DateTime>(UserExtensionConsts.CreationTimePropertyName);
            options.AddOrUpdateProperty<DateTime?>(UserExtensionConsts.LastLoginTimePropertyName);
        });
    }
}

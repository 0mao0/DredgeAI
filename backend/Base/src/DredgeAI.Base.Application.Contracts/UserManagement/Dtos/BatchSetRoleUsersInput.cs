using System.ComponentModel.DataAnnotations;

namespace DredgeAI.UserManagement;

/// <summary>批量设置角色用户输入 DTO（全量替换模式）</summary>
public class BatchSetRoleUsersInput
{
    /// <summary>角色名称</summary>
    [Required]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>期望的用户 ID 列表（全量替换，空列表表示清空所有用户）</summary>
    [Required]
    public List<Guid> UserIds { get; set; } = [];
}

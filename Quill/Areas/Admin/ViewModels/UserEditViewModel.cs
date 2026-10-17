using System.ComponentModel.DataAnnotations;
using Quill.Models;

namespace Quill.Areas.Admin.ViewModels;

/// <summary>
/// 后台编辑用户的表单模型（避免直接绑定实体带来的过度提交风险）。
/// </summary>
public class UserEditViewModel
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    [Display(Name = "显示名称")]
    [StringLength(50, ErrorMessage = "显示名称最多 50 个字符")]
    public string? DisplayName { get; set; }

    [Display(Name = "邮箱")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(120, ErrorMessage = "邮箱最多 120 个字符")]
    public string? Email { get; set; }

    [Display(Name = "头像地址")]
    [StringLength(500, ErrorMessage = "头像地址最多 500 个字符")]
    public string? Avatar { get; set; }

    [Display(Name = "个人简介")]
    public string? Bio { get; set; }

    [Display(Name = "启用账号")]
    public bool IsActive { get; set; }

    /// <summary>选中的角色（admin / editor / writer / visitor）</summary>
    [Display(Name = "角色")]
    public List<string> Roles { get; set; } = new();

    [Display(Name = "新密码")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度需在 6 到 100 位之间")]
    public string? NewPassword { get; set; }
}

/// <summary>角色展示用的中文名与徽标样式</summary>
public static class RoleNames
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        [Roles.Admin] = "管理员",
        [Roles.Editor] = "编辑",
        [Roles.Writer] = "作者",
        [Roles.Visitor] = "访客"
    };

    private static readonly Dictionary<string, string> Badges = new()
    {
        [Roles.Admin] = "bg-danger",
        [Roles.Editor] = "bg-primary",
        [Roles.Writer] = "bg-info text-dark",
        [Roles.Visitor] = "bg-secondary"
    };

    public static string Text(string role) => Texts.TryGetValue(role, out var v) ? v : role;

    public static string Badge(string role) => Badges.TryGetValue(role, out var v) ? v : "bg-secondary";
}

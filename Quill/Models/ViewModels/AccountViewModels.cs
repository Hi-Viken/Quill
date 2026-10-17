using System.ComponentModel.DataAnnotations;

namespace Quill.Models.ViewModels;

/// <summary>登录表单</summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "请输入用户名或邮箱")]
    [Display(Name = "用户名或邮箱")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "记住我")]
    public bool RememberMe { get; set; } = true;
}

/// <summary>注册表单</summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "请输入用户名")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "用户名长度需在 3-50 之间")]
    [Display(Name = "用户名")]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [Display(Name = "邮箱")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "请输入密码")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密码至少 6 位")]
    [DataType(DataType.Password)]
    [Display(Name = "密码")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致")]
    [DataType(DataType.Password)]
    [Display(Name = "确认密码")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>个人资料表单</summary>
public class ProfileViewModel
{
    [Display(Name = "用户名")]
    public string UserName { get; set; } = string.Empty;

    [Display(Name = "显示名称")]
    public string? DisplayName { get; set; }

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [Display(Name = "邮箱")]
    public string? Email { get; set; }

    [Display(Name = "头像地址")]
    public string? Avatar { get; set; }

    [Display(Name = "个人简介")]
    public string? Bio { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "当前密码")]
    public string? CurrentPassword { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "新密码至少 6 位")]
    [DataType(DataType.Password)]
    [Display(Name = "新密码")]
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密码不一致")]
    [DataType(DataType.Password)]
    [Display(Name = "确认新密码")]
    public string? ConfirmNewPassword { get; set; }
}

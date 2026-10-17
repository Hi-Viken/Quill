using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 系统用户。角色以逗号分隔字符串存储在 Roles 字段（如 "admin,writer"）。
/// 鉴权使用 ASP.NET Core Cookie 认证，不依赖 EF 版 Identity。
/// </summary>
[SugarTable("users")]
public class ApplicationUser
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DisplayName { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Avatar { get; set; }

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? Bio { get; set; }

    /// <summary>逗号分隔的多角色，如 "admin,writer"</summary>
    [SugarColumn(Length = 120, IsNullable = true)]
    public string? Roles { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public string DisplayNameOrName => string.IsNullOrWhiteSpace(DisplayName) ? UserName : DisplayName;

    [SugarColumn(IsIgnore = true)]
    public List<string> RoleList =>
        string.IsNullOrWhiteSpace(Roles)
            ? new List<string>()
            : Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

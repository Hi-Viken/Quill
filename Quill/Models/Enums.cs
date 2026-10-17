namespace Quill.Models;

/// <summary>
/// 文章状态：草稿 / 已发布
/// </summary>
public enum ArticleStatus
{
    Draft = 0,
    Published = 1
}

/// <summary>
/// 系统角色常量（多角色以逗号分隔存于 ApplicationUser.Roles）
/// </summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string Editor = "editor";
    public const string Writer = "writer";
    public const string Visitor = "visitor";

    public static readonly string[] All = { Admin, Editor, Writer, Visitor };
}

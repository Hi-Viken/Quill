using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 评论。支持树形回复（ParentId），可作用于文章或独立页面。
/// 游客评论填 AuthorName/Email，登录用户额外记录 UserId。
/// </summary>
[SugarTable("comments")]
public class Comment
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ArticleId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? PageId { get; set; }

    /// <summary>回复的父评论 Id，一级评论为 null</summary>
    [SugarColumn(IsNullable = true)]
    public int? ParentId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? UserId { get; set; }

    [SugarColumn(Length = 60, IsNullable = false)]
    public string AuthorName { get; set; } = "匿名访客";

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Website { get; set; }

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    /// <summary>审核通过标记；站点开启审核时为 false</summary>
    public bool IsApproved { get; set; } = false;

    [SugarColumn(Length = 60, IsNullable = true)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public List<Comment> Replies { get; set; } = new();

    [SugarColumn(IsIgnore = true)]
    public ApplicationUser? User { get; set; }

    [SugarColumn(IsIgnore = true)]
    public Article? Article { get; set; }
}

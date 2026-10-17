using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 点赞记录。登录用户按 UserId 去重，游客按 IP 去重。
/// </summary>
[SugarTable("likes")]
public class Like
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ArticleId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? PageId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? UserId { get; set; }

    [SugarColumn(Length = 60, IsNullable = true)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

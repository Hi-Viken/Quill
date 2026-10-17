using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文章标签
/// </summary>
[SugarTable("tags")]
public class Tag
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? Slug { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int ArticleCount { get; set; }
}

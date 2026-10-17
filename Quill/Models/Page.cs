using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 独立页面（如"关于我"），不参与分类/标签/归档体系
/// </summary>
[SugarTable("pages")]
public class Page
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(Length = 220, IsNullable = true)]
    public string? Slug { get; set; }

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = false)]
    public string ContentMarkdown { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? ContentHtml { get; set; }

    public bool IsPublished { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    public bool AllowComment { get; set; } = false;

    public int AuthorId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public ApplicationUser? Author { get; set; }
}

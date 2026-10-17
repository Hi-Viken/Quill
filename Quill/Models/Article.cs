using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文章。正文以 Markdown 存储，发布时预渲染为 ContentHtml；支持草稿/发布/置顶与封面图。
/// </summary>
[SugarTable("articles")]
public class Article
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

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? Summary { get; set; }

    /// <summary>封面图地址</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? CoverImage { get; set; }

    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    /// <summary>是否置顶</summary>
    public bool IsPinned { get; set; } = false;

    public bool AllowComment { get; set; } = true;

    public int AuthorId { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? CategoryId { get; set; }

    public int ViewCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public ApplicationUser? Author { get; set; }

    [SugarColumn(IsIgnore = true)]
    public Category? Category { get; set; }

    [SugarColumn(IsIgnore = true)]
    public List<Tag> Tags { get; set; } = new();

    [SugarColumn(IsIgnore = true)]
    public int CommentCount { get; set; }

    [SugarColumn(IsIgnore = true)]
    public int LikeCount { get; set; }
}

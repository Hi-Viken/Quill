using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文章分类。支持多级（ParentId），并提供 SortOrder 排序字段。
/// </summary>
[SugarTable("categories")]
public class Category
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? Slug { get; set; }

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>排序权重，数字越小越靠前</summary>
    public int SortOrder { get; set; } = 0;

    [SugarColumn(IsNullable = true)]
    public int? ParentId { get; set; }

    [SugarColumn(IsIgnore = true)]
    public List<Category> Children { get; set; } = new();

    [SugarColumn(IsIgnore = true)]
    public int ArticleCount { get; set; }

    [SugarColumn(IsIgnore = true)]
    public string FullName => ParentId.HasValue ? $"{ParentName}/{Name}" : Name;

    [SugarColumn(IsIgnore = true)]
    public string? ParentName { get; set; }
}

using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文章与标签的多对多关联
/// </summary>
[SugarTable("article_tags")]
public class ArticleTag
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public int TagId { get; set; }
}

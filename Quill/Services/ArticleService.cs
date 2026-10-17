using Quill.Models;
using Quill.Utils;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 文章服务：前台分页/查询、归档、相关文章，以及后台的保存与删除。
/// </summary>
public class ArticleService
{
    private readonly ISqlSugarClient _db;
    private readonly MarkdownService _md;

    public ArticleService(ISqlSugarClient db, MarkdownService md)
    {
        _db = db;
        _md = md;
    }

    // ---------- 基础查询构造 ----------

    private ISugarQueryable<Article> Published() =>
        _db.Queryable<Article>().Where(a => a.Status == ArticleStatus.Published);

    private static ISugarQueryable<Article> WithDefaultOrder(ISugarQueryable<Article> q) =>
        q.OrderBy(a => a.IsPinned, OrderByType.Desc)
         .OrderBy(a => a.PublishedAt, OrderByType.Desc)
         .OrderBy(a => a.Id, OrderByType.Desc);

    // ---------- 前台分页 ----------

    public PagedResult<Article> GetPublished(int page = 1, int pageSize = 10)
    {
        var total = 0;
        var items = WithDefaultOrder(Published()).ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public PagedResult<Article> GetByCategory(int categoryId, int page = 1, int pageSize = 10)
    {
        var total = 0;
        var items = WithDefaultOrder(Published().Where(a => a.CategoryId == categoryId))
            .ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public PagedResult<Article> GetByTag(int tagId, int page = 1, int pageSize = 10)
    {
        var articleIds = _db.Queryable<ArticleTag>().Where(t => t.TagId == tagId).Select(t => t.ArticleId).ToList();
        if (articleIds.Count == 0)
            return new PagedResult<Article> { Page = page, PageSize = pageSize };

        var total = 0;
        var items = WithDefaultOrder(Published().Where(a => articleIds.Contains(a.Id)))
            .ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public PagedResult<Article> GetByAuthor(int authorId, int page = 1, int pageSize = 10)
    {
        var total = 0;
        var items = WithDefaultOrder(Published().Where(a => a.AuthorId == authorId))
            .ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>按年/月归档查询；month 为空表示查整年</summary>
    public PagedResult<Article> GetByArchive(int year, int? month, int page = 1, int pageSize = 10)
    {
        var start = new DateTime(year, month ?? 1, 1);
        var end = month.HasValue ? start.AddMonths(1) : start.AddYears(1);

        var total = 0;
        var items = WithDefaultOrder(Published().Where(a => a.PublishedAt >= start && a.PublishedAt < end))
            .ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public PagedResult<Article> Search(string keyword, int page = 1, int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return new PagedResult<Article> { Page = page, PageSize = pageSize };

        var kw = keyword.Trim();
        var total = 0;
        var items = WithDefaultOrder(
                Published().Where(a => a.Title.Contains(kw) || (a.Summary ?? string.Empty).Contains(kw) || a.ContentMarkdown.Contains(kw)))
            .ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    // ---------- 单篇 ----------

    public Article? GetBySlug(string slug)
    {
        var a = _db.Queryable<Article>().First(x => x.Slug == slug);
        if (a != null) LoadExtras(new List<Article> { a });
        return a;
    }

    public Article? GetById(int id)
    {
        var a = _db.Queryable<Article>().First(x => x.Id == id);
        if (a != null) LoadExtras(new List<Article> { a });
        return a;
    }

    /// <summary>后台编辑用：不限制状态</summary>
    public Article? GetForEdit(int id)
    {
        var a = _db.Queryable<Article>().First(x => x.Id == id);
        return a;
    }

    public void IncrementView(int id)
    {
        _db.Updateable<Article>()
           .SetColumns(a => a.ViewCount == a.ViewCount + 1)
           .Where(a => a.Id == id)
           .ExecuteCommand();
    }

    public List<Article> GetRelated(Article article, int take = 5)
    {
        var q = Published().Where(a => a.Id != article.Id);
        if (article.CategoryId.HasValue)
        {
            var catId = article.CategoryId.Value;
            q = q.Where(a => a.CategoryId == catId);
        }
        var items = WithDefaultOrder(q).Take(take).ToList();
        LoadExtras(items);
        return items;
    }

    public List<Article> GetLatest(int take = 5)
    {
        var items = WithDefaultOrder(Published()).Take(take).ToList();
        LoadExtras(items);
        return items;
    }

    public List<Article> GetMostViewed(int take = 5)
    {
        var items = Published().OrderBy(a => a.ViewCount, OrderByType.Desc).Take(take).ToList();
        LoadExtras(items);
        return items;
    }

    /// <summary>归档分组（按年月倒序）</summary>
    public List<ArchiveGroup> GetArchives()
    {
        var dates = Published()
            .Where(a => a.PublishedAt != null)
            .Select(a => a.PublishedAt!.Value)
            .ToList();

        return dates
            .GroupBy(d => new { d.Year, d.Month })
            .Select(g => new ArchiveGroup { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .ToList();
    }

    // ---------- 后台管理 ----------

    /// <summary>后台列表（含草稿），支持状态/分类/关键字筛选</summary>
    public PagedResult<Article> GetForAdmin(int page, int pageSize, ArticleStatus? status = null,
        int? categoryId = null, string? keyword = null, int? authorId = null)
    {
        var q = _db.Queryable<Article>();
        if (status.HasValue) q = q.Where(a => a.Status == status.Value);
        if (categoryId.HasValue) q = q.Where(a => a.CategoryId == categoryId.Value);
        if (authorId.HasValue) q = q.Where(a => a.AuthorId == authorId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(a => a.Title.Contains(keyword.Trim()));

        var total = 0;
        var items = q.OrderBy(a => a.IsPinned, OrderByType.Desc)
                     .OrderBy(a => a.CreatedAt, OrderByType.Desc)
                     .ToPageList(page, pageSize, ref total);
        LoadExtras(items);
        return new PagedResult<Article> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>保存文章（含 Markdown 预渲染、slug 生成、标签同步）</summary>
    public void Save(Article article, List<int> tagIds)
    {
        article.UpdatedAt = DateTime.Now;
        article.ContentHtml = _md.ToHtml(article.ContentMarkdown);
        if (string.IsNullOrWhiteSpace(article.Summary))
            article.Summary = _md.ToSummary(article.ContentMarkdown);
        if (article.Status == ArticleStatus.Published && article.PublishedAt == null)
            article.PublishedAt = DateTime.Now;

        if (article.Id == 0)
        {
            article.CreatedAt = DateTime.Now;
            article.Id = _db.Insertable(article).ExecuteReturnIdentity();
        }

        article.Slug = SlugHelper.Ensure(article.Slug, "post", article.Id);
        _db.Updateable(article).ExecuteCommand();

        SyncTags(article.Id, tagIds);
    }

    private void SyncTags(int articleId, List<int>? tagIds)
    {
        _db.Deleteable<ArticleTag>().Where(t => t.ArticleId == articleId).ExecuteCommand();
        if (tagIds == null || tagIds.Count == 0) return;
        var list = tagIds.Distinct().Select(id => new ArticleTag { ArticleId = articleId, TagId = id }).ToList();
        _db.Insertable(list).ExecuteCommand();
    }

    public List<int> GetTagIds(int articleId) =>
        _db.Queryable<ArticleTag>().Where(t => t.ArticleId == articleId).Select(t => t.TagId).ToList();

    public void Delete(int id)
    {
        _db.Deleteable<Article>().Where(a => a.Id == id).ExecuteCommand();
        _db.Deleteable<ArticleTag>().Where(t => t.ArticleId == id).ExecuteCommand();
        _db.Deleteable<Comment>().Where(c => c.ArticleId == id).ExecuteCommand();
        _db.Deleteable<Like>().Where(l => l.ArticleId == id).ExecuteCommand();
    }

    // ---------- 关联数据批量加载 ----------

    /// <summary>
    /// 为文章列表批量装配作者、分类、标签、评论数、点赞数，避免 N+1 查询。
    /// </summary>
    private void LoadExtras(List<Article> articles)
    {
        if (articles == null || articles.Count == 0) return;
        var ids = articles.Select(a => a.Id).Distinct().ToList();

        // 作者
        var authorIds = articles.Select(a => a.AuthorId).Distinct().ToList();
        var users = _db.Queryable<ApplicationUser>().Where(u => authorIds.Contains(u.Id)).ToList();
        var userDict = users.ToDictionary(u => u.Id);

        // 分类
        var catIds = articles.Where(a => a.CategoryId.HasValue).Select(a => a.CategoryId!.Value).Distinct().ToList();
        var cats = catIds.Count > 0
            ? _db.Queryable<Category>().Where(c => catIds.Contains(c.Id)).ToList()
            : new List<Category>();
        var catDict = cats.ToDictionary(c => c.Id);

        // 标签（多对多）
        var links = _db.Queryable<ArticleTag>().Where(t => ids.Contains(t.ArticleId)).ToList();
        var tagIds = links.Select(t => t.TagId).Distinct().ToList();
        var tags = tagIds.Count > 0
            ? _db.Queryable<Tag>().Where(t => tagIds.Contains(t.Id)).ToList()
            : new List<Tag>();
        var tagDict = tags.ToDictionary(t => t.Id);

        // 评论数（仅已审核）
        var commentCounts = _db.Queryable<Comment>()
            .Where(c => c.IsApproved)
            .GroupBy(c => c.ArticleId)
            .Select(c => new { ArticleId = c.ArticleId, Cnt = SqlFunc.AggregateCount(c.Id) })
            .ToList();
        var ccDict = commentCounts.Where(x => x.ArticleId.HasValue)
            .ToDictionary(x => x.ArticleId!.Value, x => x.Cnt);

        // 点赞数
        var likeCounts = _db.Queryable<Like>()
            .Where(l => l.ArticleId != null)
            .GroupBy(l => l.ArticleId)
            .Select(l => new { ArticleId = l.ArticleId, Cnt = SqlFunc.AggregateCount(l.Id) })
            .ToList();
        var lcDict = likeCounts.Where(x => x.ArticleId.HasValue)
            .ToDictionary(x => x.ArticleId!.Value, x => x.Cnt);

        foreach (var a in articles)
        {
            a.Author = userDict.GetValueOrDefault(a.AuthorId);
            if (a.CategoryId.HasValue) a.Category = catDict.GetValueOrDefault(a.CategoryId.Value);
            a.Tags = links.Where(l => l.ArticleId == a.Id)
                          .Select(l => tagDict.GetValueOrDefault(l.TagId))
                          .Where(t => t != null)
                          .Select(t => t!)
                          .ToList();
            a.CommentCount = ccDict.GetValueOrDefault(a.Id);
            a.LikeCount = lcDict.GetValueOrDefault(a.Id);
        }
    }
}

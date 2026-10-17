using Quill.Models;
using Quill.Utils;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 分类与标签服务（含文章计数与分类树）
/// </summary>
public class TaxonomyService
{
    private readonly ISqlSugarClient _db;

    public TaxonomyService(ISqlSugarClient db)
    {
        _db = db;
    }

    // ================= 分类 =================

    /// <summary>分类列表（含文章数）。publishedOnly=true 时只统计已发布文章</summary>
    public List<Category> GetCategoriesWithCount(bool publishedOnly = true)
    {
        var cats = _db.Queryable<Category>().OrderBy(c => c.SortOrder).ToList();
        var dict = GetCategoryCountDict(publishedOnly);
        foreach (var c in cats) c.ArticleCount = dict.GetValueOrDefault(c.Id);
        return cats;
    }

    private Dictionary<int, int> GetCategoryCountDict(bool publishedOnly)
    {
        var q = _db.Queryable<Article>().Where(a => a.CategoryId != null);
        if (publishedOnly) q = q.Where(a => a.Status == ArticleStatus.Published);

        var rows = q.GroupBy(a => a.CategoryId)
                    .Select(a => new { CatId = a.CategoryId, Cnt = SqlFunc.AggregateCount(a.Id) })
                    .ToList();
        return rows.Where(x => x.CatId.HasValue).ToDictionary(x => x.CatId!.Value, x => x.Cnt);
    }

    /// <summary>构建多级分类树（ParentId 递归）</summary>
    public List<Category> GetCategoryTree(bool publishedOnly = true)
    {
        var all = GetCategoriesWithCount(publishedOnly);
        var roots = all.Where(c => c.ParentId == null || c.ParentId == 0).ToList();
        BuildChildren(roots, all);
        return roots;
    }

    private static void BuildChildren(List<Category> parents, List<Category> all)
    {
        foreach (var p in parents)
        {
            var children = all.Where(c => c.ParentId == p.Id).ToList();
            if (children.Count > 0)
            {
                BuildChildren(children, all);
                p.Children = children;
            }
        }
    }

    public Category? GetCategoryById(int id) => _db.Queryable<Category>().First(c => c.Id == id);

    public Category? GetCategoryBySlug(string slug) => _db.Queryable<Category>().First(c => c.Slug == slug);

    public void SaveCategory(Category c)
    {
        c.Slug = SlugHelper.Ensure(c.Slug, "cat", c.Id == 0 ? _db.Queryable<Category>().Count() + 1 : c.Id);
        if (c.Id == 0)
        {
            c.Id = _db.Insertable(c).ExecuteReturnIdentity();
            c.Slug = SlugHelper.Ensure(c.Slug, "cat", c.Id);
            _db.Updateable(c).ExecuteCommand();
        }
        else
        {
            _db.Updateable(c).ExecuteCommand();
        }
    }

    /// <summary>删除分类，其下文章的分类置空</summary>
    public void DeleteCategory(int id)
    {
        _db.Updateable<Article>()
           .SetColumns(a => a.CategoryId == null)
           .Where(a => a.CategoryId == id)
           .ExecuteCommand();
        _db.Updateable<Category>()
           .SetColumns(c => c.ParentId == null)
           .Where(c => c.ParentId == id)
           .ExecuteCommand();
        _db.Deleteable<Category>().Where(c => c.Id == id).ExecuteCommand();
    }

    // ================= 标签 =================

    public List<Tag> GetTagsWithCount(bool publishedOnly = true)
    {
        var tags = _db.Queryable<Tag>().OrderBy(t => t.Id).ToList();
        var dict = GetTagCountDict(publishedOnly);
        foreach (var t in tags) t.ArticleCount = dict.GetValueOrDefault(t.Id);
        return tags;
    }

    private Dictionary<int, int> GetTagCountDict(bool publishedOnly)
    {
        var q = _db.Queryable<ArticleTag>()
            .LeftJoin<Article>((at, a) => at.ArticleId == a.Id);
        if (publishedOnly) q = q.Where((at, a) => a.Status == ArticleStatus.Published);

        var rows = q.GroupBy((at, a) => at.TagId)
                    .Select((at, a) => new { TagId = at.TagId, Cnt = SqlFunc.AggregateCount(at.Id) })
                    .ToList();
        return rows.ToDictionary(x => x.TagId, x => x.Cnt);
    }

    public Tag? GetTagById(int id) => _db.Queryable<Tag>().First(t => t.Id == id);

    public Tag? GetTagBySlug(string slug) => _db.Queryable<Tag>().First(t => t.Slug == slug);

    public void SaveTag(Tag t)
    {
        if (t.Id == 0)
        {
            t.Id = _db.Insertable(t).ExecuteReturnIdentity();
            t.Slug = SlugHelper.Ensure(t.Slug, "tag", t.Id);
            _db.Updateable(t).ExecuteCommand();
        }
        else
        {
            t.Slug = SlugHelper.Ensure(t.Slug, "tag", t.Id);
            _db.Updateable(t).ExecuteCommand();
        }
    }

    public void DeleteTag(int id)
    {
        _db.Deleteable<ArticleTag>().Where(t => t.TagId == id).ExecuteCommand();
        _db.Deleteable<Tag>().Where(t => t.Id == id).ExecuteCommand();
    }

    /// <summary>标签云（按文章数倒序取前 N）</summary>
    public List<Tag> GetTagCloud(int take = 30) =>
        GetTagsWithCount().OrderByDescending(t => t.ArticleCount).Take(take).ToList();
}

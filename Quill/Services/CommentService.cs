using Quill.Models;
using Quill.Utils;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 评论服务：发表（含回复）、审核、树形装配
/// </summary>
public class CommentService
{
    private readonly ISqlSugarClient _db;
    private readonly SettingsService _settings;

    public CommentService(ISqlSugarClient db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    /// <summary>
    /// 发表评论/回复。是否需要审核由站点设置决定。
    /// </summary>
    public Comment Add(int? articleId, int? pageId, int? parentId, int? userId,
        string authorName, string? email, string? website, string content, string? ip)
    {
        var s = _settings.Get();
        var c = new Comment
        {
            ArticleId = articleId,
            PageId = pageId,
            ParentId = parentId,
            UserId = userId,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "匿名访客" : authorName.Trim(),
            Email = email,
            Website = website,
            Content = content.Trim(),
            IpAddress = ip,
            IsApproved = !s.RequireCommentApproval,
            CreatedAt = DateTime.Now
        };
        c.Id = _db.Insertable(c).ExecuteReturnIdentity();
        return c;
    }

    /// <summary>文章下已审核评论（树形，Replies 嵌套）</summary>
    public List<Comment> GetApprovedTree(int articleId)
    {
        var flat = _db.Queryable<Comment>()
            .Where(c => c.ArticleId == articleId && c.IsApproved)
            .OrderBy(c => c.CreatedAt)
            .ToList();
        return BuildTree(flat);
    }

    /// <summary>页面下已审核评论（树形）</summary>
    public List<Comment> GetApprovedTreeForPage(int pageId)
    {
        var flat = _db.Queryable<Comment>()
            .Where(c => c.PageId == pageId && c.IsApproved)
            .OrderBy(c => c.CreatedAt)
            .ToList();
        return BuildTree(flat);
    }

    /// <summary>将平铺评论列表装配为树（支持多层级回复）</summary>
    public static List<Comment> BuildTree(List<Comment> flat)
    {
        if (flat == null || flat.Count == 0) return new List<Comment>();

        // 顶层：ParentId 为空
        var roots = flat.Where(c => c.ParentId == null || c.ParentId == 0).ToList();
        var lookup = flat.ToLookup(c => c.ParentId ?? 0);

        foreach (var r in roots) r.Replies = BuildReplies(r.Id, lookup);
        return roots;
    }

    private static List<Comment> BuildReplies(int parentId, ILookup<int, Comment> lookup)
    {
        var children = lookup.Contains(parentId) ? lookup[parentId].ToList() : new List<Comment>();
        foreach (var c in children) c.Replies = BuildReplies(c.Id, lookup);
        return children;
    }

    public int GetPendingCount() => _db.Queryable<Comment>().Count(c => !c.IsApproved);

    public int GetTotalCount() => _db.Queryable<Comment>().Count();

    /// <summary>后台评论列表（可按审核状态筛选）</summary>
    public PagedResult<Comment> GetForAdmin(int page, int pageSize, bool? approved = null, string? keyword = null)
    {
        var q = _db.Queryable<Comment>();
        if (approved.HasValue) q = q.Where(c => c.IsApproved == approved.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(c => c.Content.Contains(keyword.Trim()) || c.AuthorName.Contains(keyword.Trim()));

        var total = 0;
        var items = q.OrderBy(c => c.CreatedAt, OrderByType.Desc).ToPageList(page, pageSize, ref total);

        // 装配所属文章标题（后台展示用）
        var articleIds = items.Where(c => c.ArticleId.HasValue).Select(c => c.ArticleId!.Value).Distinct().ToList();
        if (articleIds.Count > 0)
        {
            var titles = _db.Queryable<Article>()
                .Where(a => articleIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Title })
                .ToList();
            var dict = titles.ToDictionary(t => t.Id, t => t.Title);
            foreach (var c in items)
                if (c.ArticleId.HasValue && dict.ContainsKey(c.ArticleId.Value))
                    c.Article = new Article { Id = c.ArticleId.Value, Title = dict[c.ArticleId.Value] };
        }

        return new PagedResult<Comment> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public void Approve(int id) =>
        _db.Updateable<Comment>().SetColumns(c => c.IsApproved == true).Where(c => c.Id == id).ExecuteCommand();

    /// <summary>删除评论及其所有子回复</summary>
    public void Delete(int id)
    {
        var all = _db.Queryable<Comment>().ToList();
        var idsToDelete = new List<int> { id };
        CollectChildren(id, all, idsToDelete);
        _db.Deleteable<Comment>().Where(c => idsToDelete.Contains(c.Id)).ExecuteCommand();
    }

    private static void CollectChildren(int parentId, List<Comment> all, List<int> acc)
    {
        foreach (var c in all.Where(c => c.ParentId == parentId))
        {
            if (acc.Contains(c.Id)) continue; // 防御脏数据造成的环
            acc.Add(c.Id);
            CollectChildren(c.Id, all, acc);
        }
    }
}

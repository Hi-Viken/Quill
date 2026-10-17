using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台仪表盘：站点数据概览。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanManageContent")]
public class DashboardController : Controller
{
    private readonly ISqlSugarClient _db;
    private readonly CommentService _comments;
    private readonly UserService _users;

    public DashboardController(ISqlSugarClient db, CommentService comments, UserService users)
    {
        _db = db;
        _comments = comments;
        _users = users;
    }

    public IActionResult Index()
    {
        var totalArticles = _db.Queryable<Article>().Count();
        var published = _db.Queryable<Article>().Count(a => a.Status == ArticleStatus.Published);
        var pending = _comments.GetPendingCount();

        ViewBag.ArticleTotal = totalArticles;
        ViewBag.ArticlePublished = published;
        ViewBag.ArticleDraft = totalArticles - published;
        ViewBag.CommentTotal = _comments.GetTotalCount();
        ViewBag.CommentPending = pending;
        ViewBag.UserCount = _users.GetAll().Count;
        ViewBag.CategoryCount = _db.Queryable<Category>().Count();
        ViewBag.TagCount = _db.Queryable<Tag>().Count();

        // 最近文章（一次性装配作者，避免 N+1）
        var recent = _db.Queryable<Article>()
            .OrderBy(a => a.CreatedAt, OrderByType.Desc)
            .Take(8)
            .ToList();
        var authorIds = recent.Select(a => a.AuthorId).Distinct().ToList();
        var authorDict = authorIds.Count == 0
            ? new Dictionary<int, ApplicationUser>()
            : _db.Queryable<ApplicationUser>().Where(u => authorIds.Contains(u.Id)).ToList().ToDictionary(u => u.Id);
        foreach (var a in recent) a.Author = authorDict.GetValueOrDefault(a.AuthorId);
        ViewBag.RecentArticles = recent;

        ViewBag.PendingComments = _comments.GetForAdmin(1, 8, false).Items;

        return View();
    }
}

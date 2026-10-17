using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;
using SqlSugar;

namespace Quill.Controllers;

/// <summary>
/// 作者：列出有已发布文章的作者及其文章
/// </summary>
public class AuthorController : Controller
{
    private readonly ISqlSugarClient _db;
    private readonly ArticleService _articles;
    private readonly SettingsService _settings;

    public AuthorController(ISqlSugarClient db, ArticleService articles, SettingsService settings)
    {
        _db = db;
        _articles = articles;
        _settings = settings;
    }

    /// <summary>全部作者（含文章数）</summary>
    public IActionResult Index()
    {
        ViewData["Title"] = "作者";

        var rows = _db.Queryable<Article>()
            .Where(a => a.Status == ArticleStatus.Published)
            .GroupBy(a => a.AuthorId)
            .Select(a => new { AuthorId = a.AuthorId, Cnt = SqlFunc.AggregateCount(a.Id) })
            .ToList();

        if (rows.Count == 0)
        {
            ViewBag.ArticleCounts = new Dictionary<int, int>();
            return View(new List<ApplicationUser>());
        }

        var ids = rows.Select(r => r.AuthorId).ToList();
        var users = _db.Queryable<ApplicationUser>().Where(u => ids.Contains(u.Id)).ToList();
        ViewBag.ArticleCounts = rows.ToDictionary(r => r.AuthorId, r => r.Cnt);
        return View(users);
    }

    /// <summary>某作者的文章列表</summary>
    public IActionResult Details(int id, int page = 1)
    {
        var author = _db.Queryable<ApplicationUser>().First(u => u.Id == id);
        if (author == null) return NotFound();

        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        var result = _articles.GetByAuthor(id, page, pageSize);

        ViewBag.Author = author;
        ViewData["Title"] = $"作者：{author.DisplayNameOrName}";
        return View(result);
    }
}

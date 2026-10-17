using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;
using SqlSugar;

namespace Quill.Controllers;

/// <summary>
/// 独立页面（如"关于我"）前台展示
/// </summary>
public class PageController : Controller
{
    private readonly ISqlSugarClient _db;
    private readonly MarkdownService _md;
    private readonly CommentService _comments;
    private readonly SettingsService _settings;

    public PageController(ISqlSugarClient db, MarkdownService md, CommentService comments, SettingsService settings)
    {
        _db = db;
        _md = md;
        _comments = comments;
        _settings = settings;
    }

    /// <summary>按 slug 展示独立页面</summary>
    public IActionResult Details(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();

        var page = _db.Queryable<Quill.Models.Page>().First(p => p.Slug == slug && p.IsPublished);
        if (page == null) return NotFound();

        if (string.IsNullOrEmpty(page.ContentHtml))
            page.ContentHtml = _md.ToHtml(page.ContentMarkdown);

        page.Author = _db.Queryable<ApplicationUser>().First(u => u.Id == page.AuthorId);

        ViewBag.Comments = page.AllowComment
            ? _comments.GetApprovedTreeForPage(page.Id)
            : new List<Comment>();
        ViewBag.AllowComment = page.AllowComment;
        ViewData["Title"] = page.Title;

        return View(page);
    }
}

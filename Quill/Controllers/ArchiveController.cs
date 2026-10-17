using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;
using SqlSugar;

namespace Quill.Controllers;

/// <summary>
/// 归档：按年/月浏览历史文章
/// </summary>
public class ArchiveController : Controller
{
    private readonly ArticleService _articles;
    private readonly SettingsService _settings;

    public ArchiveController(ArticleService articles, SettingsService settings)
    {
        _articles = articles;
        _settings = settings;
    }

    /// <summary>归档总览（按年月分组）</summary>
    public IActionResult Index()
    {
        ViewData["Title"] = "文章归档";
        var groups = _articles.GetArchives();
        return View(groups);
    }

    /// <summary>指定年/月的文章；month 为 0 表示整年</summary>
    public IActionResult Details(int year, int month = 0, int page = 1)
    {
        if (year <= 0 || year > 9999) return NotFound();
        if (month < 0 || month > 12) return NotFound();

        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        int? m = month == 0 ? null : month;
        var result = _articles.GetByArchive(year, m, page, pageSize);

        ViewBag.Year = year;
        ViewBag.Month = month;
        ViewData["Title"] = m.HasValue ? $"{year}年{month:D2}月归档" : $"{year}年归档";
        return View(result);
    }
}

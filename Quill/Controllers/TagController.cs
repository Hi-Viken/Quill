using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 标签浏览与标签云
/// </summary>
public class TagController : Controller
{
    private readonly ArticleService _articles;
    private readonly TaxonomyService _taxonomy;
    private readonly SettingsService _settings;

    public TagController(ArticleService articles, TaxonomyService taxonomy, SettingsService settings)
    {
        _articles = articles;
        _taxonomy = taxonomy;
        _settings = settings;
    }

    /// <summary>标签云</summary>
    public IActionResult Index()
    {
        ViewData["Title"] = "标签云";
        var tags = _taxonomy.GetTagsWithCount().OrderByDescending(t => t.ArticleCount).ToList();
        return View(tags);
    }

    /// <summary>某标签下的文章</summary>
    public IActionResult Details(string slug, int page = 1)
    {
        var tag = _taxonomy.GetTagBySlug(slug);
        if (tag == null) return NotFound();

        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        var result = _articles.GetByTag(tag.Id, page, pageSize);

        ViewBag.Tag = tag;
        ViewData["Title"] = $"标签：{tag.Name}";
        return View(result);
    }
}

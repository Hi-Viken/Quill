using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 分类浏览
/// </summary>
public class CategoryController : Controller
{
    private readonly ArticleService _articles;
    private readonly TaxonomyService _taxonomy;
    private readonly SettingsService _settings;

    public CategoryController(ArticleService articles, TaxonomyService taxonomy, SettingsService settings)
    {
        _articles = articles;
        _taxonomy = taxonomy;
        _settings = settings;
    }

    /// <summary>全部分类（含文章数）</summary>
    public IActionResult Index()
    {
        ViewData["Title"] = "全部分类";
        var cats = _taxonomy.GetCategoryTree();
        return View(cats);
    }

    /// <summary>某分类下的文章</summary>
    public IActionResult Details(string slug, int page = 1)
    {
        var cat = _taxonomy.GetCategoryBySlug(slug);
        if (cat == null) return NotFound();

        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        var result = _articles.GetByCategory(cat.Id, page, pageSize);

        ViewBag.Category = cat;
        ViewData["Title"] = $"分类：{cat.Name}";
        return View(result);
    }
}

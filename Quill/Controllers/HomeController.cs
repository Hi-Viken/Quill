using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;

namespace Quill.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ArticleService _articles;
    private readonly SettingsService _settings;

    public HomeController(ILogger<HomeController> logger, ArticleService articles, SettingsService settings)
    {
        _logger = logger;
        _articles = articles;
        _settings = settings;
    }

    /// <summary>首页：已发布文章列表（置顶优先），支持分页</summary>
    public IActionResult Index(int page = 1)
    {
        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        var result = _articles.GetPublished(page, pageSize);
        ViewData["Title"] = s.SiteName;
        return View(result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Models.ViewModels;
using Quill.Services;

namespace Quill.Controllers;

public class ArticleController : Controller
{
    private readonly ArticleService _articles;
    private readonly CommentService _comments;
    private readonly LikeService _likes;
    private readonly SettingsService _settings;
    private readonly UserService _users;

    public ArticleController(ArticleService articles, CommentService comments, LikeService likes,
        SettingsService settings, UserService users)
    {
        _articles = articles;
        _comments = comments;
        _likes = likes;
        _settings = settings;
        _users = users;
    }

    private ApplicationUser? CurrentUser =>
        User.Identity is { IsAuthenticated: true } ? _users.GetByUserName(User.Identity.Name!) : null;

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>全部文章</summary>
    public IActionResult Index(int page = 1)
    {
        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        var result = _articles.GetPublished(page, pageSize);
        ViewData["Title"] = "全部文章";
        return View(result);
    }

    /// <summary>文章详情，id 可为 slug 或数字 Id</summary>
    public IActionResult Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var article = int.TryParse(id, out var numericId)
            ? _articles.GetById(numericId)
            : _articles.GetBySlug(id);

        if (article == null) return NotFound();

        // 草稿仅管理员/编辑可预览，作者本人亦可预览自己的草稿
        if (article.Status != ArticleStatus.Published)
        {
            var me = CurrentUser;
            var canPreview = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor)
                             || (me != null && me.Id == article.AuthorId);
            if (!canPreview) return NotFound();
            ViewData["IsPreview"] = true;
        }

        var s = _settings.Get();
        var user = CurrentUser;
        var userId = user?.Id;
        var ip = ClientIp;

        _articles.IncrementView(article.Id);

        var vm = new ArticleDetailViewModel
        {
            Article = article,
            Comments = _comments.GetApprovedTree(article.Id),
            Related = _articles.GetRelated(article, 5),
            CommentCount = article.CommentCount,
            LikeCount = article.LikeCount,
            Liked = _likes.HasLiked(article.Id, userId, ip),
            CanComment = s.AllowComments && article.AllowComment,
            Input = new CommentInputModel
            {
                ArticleId = article.Id,
                AuthorName = user?.DisplayNameOrName ?? string.Empty,
                Email = user?.Email
            }
        };

        ViewData["Title"] = article.Title;
        return View(vm);
    }

    /// <summary>搜索</summary>
    public IActionResult Search(string? q, int page = 1)
    {
        var s = _settings.Get();
        var pageSize = s.PostsPerPage <= 0 ? 10 : s.PostsPerPage;
        var result = _articles.Search(q ?? string.Empty, page, pageSize);
        ViewData["Title"] = "搜索结果";
        ViewData["Keyword"] = q;
        return View(result);
    }
}

using Microsoft.AspNetCore.Mvc;
using Quill.Models.ViewModels;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 评论发表（含回复）
/// </summary>
public class CommentController : Controller
{
    private readonly CommentService _comments;
    private readonly SettingsService _settings;
    private readonly UserService _users;
    private readonly ArticleService _articles;

    public CommentController(CommentService comments, SettingsService settings,
        UserService users, ArticleService articles)
    {
        _comments = comments;
        _settings = settings;
        _users = users;
        _articles = articles;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(CommentInputModel model)
    {
        var s = _settings.Get();
        var me = User.Identity is { IsAuthenticated: true } ? _users.GetByUserName(User.Identity.Name!) : null;
        var backUrl = BuildBackUrl(model);

        if (!s.AllowComments)
        {
            TempData["Error"] = "站点已关闭评论功能";
            return Redirect(backUrl);
        }

        // 未登录且不允许游客评论 -> 引导登录
        if (me == null && !s.AllowVisitorComment)
        {
            TempData["Error"] = "请先登录后发表评论";
            return RedirectToAction("Login", "Account", new { returnUrl = backUrl });
        }

        // 已登录用户未填昵称时自动填充
        if (me != null && string.IsNullOrWhiteSpace(model.AuthorName))
            model.AuthorName = me.DisplayNameOrName;

        if (!ModelState.IsValid)
        {
            var msg = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault();
            TempData["Error"] = msg ?? "请检查评论表单填写是否完整";
            return Redirect(backUrl);
        }

        if (model.ArticleId == null && model.PageId == null)
        {
            TempData["Error"] = "评论目标不存在";
            return Redirect(backUrl);
        }

        _comments.Add(
            model.ArticleId,
            model.PageId,
            model.ParentId,
            me?.Id,
            model.AuthorName,
            model.Email,
            model.Website,
            model.Content,
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = s.RequireCommentApproval
            ? "评论已提交，等待管理员审核后显示"
            : "评论发布成功";

        return Redirect(backUrl + "#comments");
    }

    /// <summary>构造返回地址：优先文章详情，其次 Referer，最后首页</summary>
    private string BuildBackUrl(CommentInputModel model)
    {
        if (model.ArticleId.HasValue)
        {
            var a = _articles.GetForEdit(model.ArticleId.Value);
            if (a != null)
            {
                var url = Url.Action("Details", "Article", new { id = string.IsNullOrEmpty(a.Slug) ? a.Id.ToString() : a.Slug });
                if (!string.IsNullOrEmpty(url)) return url;
            }
        }

        var referer = Request.Headers["Referer"].ToString();
        return string.IsNullOrWhiteSpace(referer) ? "/" : referer;
    }
}

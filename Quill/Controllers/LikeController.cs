using Microsoft.AspNetCore.Mvc;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 点赞：登录用户按用户去重，游客按 IP 去重，可取消
/// </summary>
public class LikeController : Controller
{
    private readonly LikeService _likes;
    private readonly UserService _users;

    public LikeController(LikeService likes, UserService users)
    {
        _likes = likes;
        _users = users;
    }

    /// <summary>切换点赞状态，返回最新总数与当前状态</summary>
    [HttpPost]
    public IActionResult Toggle(int articleId)
    {
        if (articleId <= 0)
            return Json(new { success = false, message = "参数错误" });

        var me = User.Identity is { IsAuthenticated: true } ? _users.GetByUserName(User.Identity.Name!) : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (count, liked) = _likes.Toggle(articleId, me?.Id, ip);
        return Json(new { success = true, count, liked });
    }

    /// <summary>查询点赞数与当前访客是否已点赞</summary>
    [HttpGet]
    public IActionResult Status(int articleId)
    {
        var me = User.Identity is { IsAuthenticated: true } ? _users.GetByUserName(User.Identity.Name!) : null;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Json(new
        {
            count = _likes.GetCount(articleId),
            liked = _likes.HasLiked(articleId, me?.Id, ip)
        });
    }
}

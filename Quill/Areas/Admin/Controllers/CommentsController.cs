using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Quill.Models;
using Quill.Services;
using Quill.Utils;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台评论管理：审核、删除、批量审核与回复。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanManageContent")]
public class CommentsController : Controller
{
    private const int PageSize = 20;

    private readonly CommentService _comments;
    private readonly UserService _users;
    private readonly ISqlSugarClient _db;

    public CommentsController(CommentService comments, UserService users, ISqlSugarClient db)
    {
        _comments = comments;
        _users = users;
        _db = db;
    }

    public IActionResult Index(int page = 1, bool? approved = null, string? keyword = null)
    {
        var result = _comments.GetForAdmin(page, PageSize, approved, keyword);

        ViewBag.Keyword = keyword;
        ViewBag.Approved = approved.HasValue ? approved.Value.ToString().ToLowerInvariant() : string.Empty;
        ViewBag.ApprovedList = new List<SelectListItem>
        {
            new() { Value = "", Text = "全部评论", Selected = !approved.HasValue },
            new() { Value = "false", Text = "待审核", Selected = approved == false },
            new() { Value = "true", Text = "已通过", Selected = approved == true }
        };

        return View(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Approve(int id)
    {
        _comments.Approve(id);
        TempData["Success"] = "评论已通过";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        _comments.Delete(id);
        TempData["Success"] = "评论已删除";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BulkApprove(int[] ids)
    {
        foreach (var id in ids.Distinct())
        {
            _comments.Approve(id);
        }
        TempData["Success"] = $"已通过 {ids.Length} 条评论";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Reply(int id, string content)
    {
        var parent = _db.Queryable<Comment>().First(c => c.Id == id);
        if (parent == null) return NotFound();

        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "回复内容不能为空";
            return RedirectToAction(nameof(Index));
        }

        var me = CurrentUser();
        _db.Insertable(new Comment
        {
            ArticleId = parent.ArticleId,
            PageId = parent.PageId,
            ParentId = parent.Id,
            UserId = me?.Id,
            AuthorName = me?.DisplayNameOrName ?? "管理员",
            Email = me?.Email,
            Content = content.Trim(),
            IsApproved = true,
            CreatedAt = DateTime.Now
        }).ExecuteCommand();

        TempData["Success"] = "回复已发布";
        return RedirectToAction(nameof(Index));
    }

    private ApplicationUser? CurrentUser()
    {
        var name = User.Identity?.Name;
        return string.IsNullOrEmpty(name) ? null : _users.GetByUserName(name);
    }
}

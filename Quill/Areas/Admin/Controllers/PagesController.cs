using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;
using Quill.Utils;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台独立页面（如「关于我」）管理。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanManageContent")]
public class PagesController : Controller
{
    private readonly ISqlSugarClient _db;
    private readonly MarkdownService _md;
    private readonly UserService _users;

    public PagesController(ISqlSugarClient db, MarkdownService md, UserService users)
    {
        _db = db;
        _md = md;
        _users = users;
    }

    public IActionResult Index()
    {
        var list = _db.Queryable<Page>()
            .OrderBy(p => p.SortOrder)
            .OrderBy(p => p.Id, OrderByType.Desc)
            .ToList();
        return View(list);
    }

    public IActionResult Create()
    {
        return View(new Page { IsPublished = true, AllowComment = false, SortOrder = 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Page model)
    {
        if (!ModelState.IsValid) return View(model);

        model.AuthorId = CurrentUserId();
        model.CreatedAt = DateTime.Now;
        model.ContentHtml = _md.ToHtml(model.ContentMarkdown);
        model.UpdatedAt = DateTime.Now;

        model.Id = _db.Insertable(model).ExecuteReturnIdentity();
        model.Slug = SlugHelper.Ensure(model.Slug, "page", model.Id);
        _db.Updateable(model).ExecuteCommand();

        TempData["Success"] = "页面已创建";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var page = _db.Queryable<Page>().First(p => p.Id == id);
        if (page == null) return NotFound();
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Page model)
    {
        var page = _db.Queryable<Page>().First(p => p.Id == model.Id);
        if (page == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        page.Title = model.Title;
        page.Slug = model.Slug;
        page.ContentMarkdown = model.ContentMarkdown;
        page.ContentHtml = _md.ToHtml(model.ContentMarkdown);
        page.IsPublished = model.IsPublished;
        page.SortOrder = model.SortOrder;
        page.AllowComment = model.AllowComment;
        page.UpdatedAt = DateTime.Now;
        page.Slug = SlugHelper.Ensure(page.Slug, "page", page.Id);

        _db.Updateable(page).ExecuteCommand();

        TempData["Success"] = "页面已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        _db.Deleteable<Comment>().Where(c => c.PageId == id).ExecuteCommand();
        _db.Deleteable<Page>().Where(p => p.Id == id).ExecuteCommand();
        TempData["Success"] = "页面已删除";
        return RedirectToAction(nameof(Index));
    }

    private int CurrentUserId()
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return 0;
        return _users.GetByUserName(name)?.Id ?? 0;
    }
}

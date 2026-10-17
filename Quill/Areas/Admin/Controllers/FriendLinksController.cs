using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台友情链接管理。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class FriendLinksController : Controller
{
    private readonly ISqlSugarClient _db;

    public FriendLinksController(ISqlSugarClient db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        var list = _db.Queryable<FriendLink>()
            .OrderBy(f => f.SortOrder)
            .OrderBy(f => f.Id, OrderByType.Desc)
            .ToList();
        return View(list);
    }

    public IActionResult Create()
    {
        return View(new FriendLink { SortOrder = 0, IsVisible = true, CreatedAt = DateTime.Now });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(FriendLink model)
    {
        if (!ModelState.IsValid) return View(model);

        model.CreatedAt = DateTime.Now;
        model.Id = _db.Insertable(model).ExecuteReturnIdentity();

        TempData["Success"] = "友情链接已创建";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var link = _db.Queryable<FriendLink>().First(f => f.Id == id);
        if (link == null) return NotFound();
        return View(link);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(FriendLink model)
    {
        var link = _db.Queryable<FriendLink>().First(f => f.Id == model.Id);
        if (link == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        link.Name = model.Name;
        link.Url = model.Url;
        link.Logo = model.Logo;
        link.Description = model.Description;
        link.SortOrder = model.SortOrder;
        link.IsVisible = model.IsVisible;

        _db.Updateable(link).ExecuteCommand();

        TempData["Success"] = "友情链接已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        _db.Deleteable<FriendLink>().Where(f => f.Id == id).ExecuteCommand();
        TempData["Success"] = "友情链接已删除";
        return RedirectToAction(nameof(Index));
    }
}

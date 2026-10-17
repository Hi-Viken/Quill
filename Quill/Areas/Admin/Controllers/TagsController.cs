using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台标签管理。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanManageContent")]
public class TagsController : Controller
{
    private readonly TaxonomyService _taxonomy;

    public TagsController(TaxonomyService taxonomy)
    {
        _taxonomy = taxonomy;
    }

    public IActionResult Index()
    {
        return View(_taxonomy.GetTagsWithCount(false));
    }

    public IActionResult Create()
    {
        return View(new Tag());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Tag model)
    {
        if (!ModelState.IsValid) return View(model);

        _taxonomy.SaveTag(model);
        TempData["Success"] = "标签已创建";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var tag = _taxonomy.GetTagById(id);
        if (tag == null) return NotFound();
        return View(tag);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Tag model)
    {
        var tag = _taxonomy.GetTagById(model.Id);
        if (tag == null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        tag.Name = model.Name;
        tag.Slug = model.Slug;
        _taxonomy.SaveTag(tag);

        TempData["Success"] = "标签已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        _taxonomy.DeleteTag(id);
        TempData["Success"] = "标签已删除";
        return RedirectToAction(nameof(Index));
    }
}

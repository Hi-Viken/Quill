using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Quill.Models;
using Quill.Services;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台分类管理（支持多级父子分类）。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanManageContent")]
public class CategoriesController : Controller
{
    private readonly TaxonomyService _taxonomy;

    public CategoriesController(TaxonomyService taxonomy)
    {
        _taxonomy = taxonomy;
    }

    public IActionResult Index()
    {
        var list = FlattenForView(_taxonomy.GetCategoriesWithCount(false));
        return View(list);
    }

    public IActionResult Create()
    {
        ViewBag.ParentList = BuildParentOptions(null, null);
        return View(new Category { SortOrder = 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Category model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ParentList = BuildParentOptions(null, model.ParentId);
            return View(model);
        }

        _taxonomy.SaveCategory(model);
        TempData["Success"] = "分类已创建";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var category = _taxonomy.GetCategoryById(id);
        if (category == null) return NotFound();

        ViewBag.ParentList = BuildParentOptions(id, category.ParentId);
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Category model)
    {
        var category = _taxonomy.GetCategoryById(model.Id);
        if (category == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.ParentList = BuildParentOptions(model.Id, model.ParentId);
            return View(model);
        }

        category.Name = model.Name;
        category.Slug = model.Slug;
        category.Description = model.Description;
        category.SortOrder = model.SortOrder;
        category.ParentId = model.ParentId;

        _taxonomy.SaveCategory(category);
        TempData["Success"] = "分类已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        _taxonomy.DeleteCategory(id);
        TempData["Success"] = "分类已删除，其下文章已改为未分类";
        return RedirectToAction(nameof(Index));
    }

    // ============ 树形辅助 ============

    /// <summary>把分类树摊平为带缩进前缀的列表，供列表页展示</summary>
    private static List<Category> FlattenForView(List<Category> all)
    {
        var output = new List<Category>();
        WalkForView(all, null, output, 0);
        return output;
    }

    private static void WalkForView(List<Category> all, int? parentId, List<Category> output, int depth)
    {
        foreach (var c in ChildrenOf(all, parentId))
        {
            c.ParentName = depth == 0 ? string.Empty : new string('　', depth - 1) + "└ ";
            output.Add(c);
            WalkForView(all, c.Id, output, depth + 1);
        }
    }

    /// <summary>父级下拉选项，排除自身及其所有子孙，避免出现环</summary>
    private List<SelectListItem> BuildParentOptions(int? excludeId, int? selectedId)
    {
        var all = _taxonomy.GetCategoriesWithCount(false);

        var excluded = new HashSet<int>();
        if (excludeId.HasValue)
        {
            excluded.Add(excludeId.Value);
            CollectDescendants(all, excludeId.Value, excluded);
        }

        var options = new List<SelectListItem>
        {
            new() { Value = "", Text = "（作为顶级分类）", Selected = !selectedId.HasValue }
        };
        AppendOptions(all, null, options, string.Empty, excluded, selectedId);
        return options;
    }

    private static void AppendOptions(List<Category> all, int? parentId, List<SelectListItem> output,
        string path, HashSet<int> excluded, int? selectedId)
    {
        foreach (var c in ChildrenOf(all, parentId))
        {
            if (excluded.Contains(c.Id)) continue; // 被排除节点的子孙同样在 excluded 中
            var label = path.Length == 0 ? c.Name : path + " / " + c.Name;
            output.Add(new SelectListItem { Value = c.Id.ToString(), Text = label, Selected = selectedId == c.Id });
            AppendOptions(all, c.Id, output, label, excluded, selectedId);
        }
    }

    private static void CollectDescendants(List<Category> all, int parentId, HashSet<int> acc)
    {
        foreach (var c in ChildrenOf(all, parentId))
        {
            if (!acc.Add(c.Id)) continue;
            CollectDescendants(all, c.Id, acc);
        }
    }

    private static List<Category> ChildrenOf(List<Category> all, int? parentId) =>
        all.Where(x => (x.ParentId ?? 0) == (parentId ?? 0))
           .OrderBy(x => x.SortOrder)
           .ThenBy(x => x.Id)
           .ToList();
}

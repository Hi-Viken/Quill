using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Quill.Models;
using Quill.Services;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台导航菜单管理（支持无限层级）。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class NavMenusController : Controller
{
    private readonly ISqlSugarClient _db;
    private readonly MenuLinkResolver _links;

    public NavMenusController(ISqlSugarClient db, MenuLinkResolver links)
    {
        _db = db;
        _links = links;
    }

    public IActionResult Index()
    {
        var all = _db.Queryable<NavMenu>().OrderBy(m => m.SortOrder).ToList();
        _links.Resolve(all, includeDrafts: true);
        return View(FlattenForView(all));
    }

    public IActionResult Create()
    {
        LoadFormData(null, null, "_self");
        return View(new NavMenu { SortOrder = 0, IsVisible = true, IsExternal = false, LinkType = MenuLinkType.Url });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NavMenu model)
    {
        NormalizeLink(model);

        if (!ModelState.IsValid)
        {
            LoadFormData(null, model.ParentId, model.Target, model.LinkType, model.LinkId);
            return View(model);
        }

        model.Id = _db.Insertable(model).ExecuteReturnIdentity();
        TempData["Success"] = "菜单项已创建";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var menu = _db.Queryable<NavMenu>().First(m => m.Id == id);
        if (menu == null) return NotFound();

        _links.Resolve(new[] { menu }, includeDrafts: true);
        LoadFormData(id, menu.ParentId, menu.Target, menu.LinkType, menu.LinkId);
        return View(menu);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(NavMenu model)
    {
        var menu = _db.Queryable<NavMenu>().First(m => m.Id == model.Id);
        if (menu == null) return NotFound();

        NormalizeLink(model);

        if (!ModelState.IsValid)
        {
            LoadFormData(model.Id, model.ParentId, model.Target, model.LinkType, model.LinkId);
            return View(model);
        }

        menu.Title = model.Title;
        menu.Url = model.Url;
        menu.ParentId = model.ParentId;
        menu.SortOrder = model.SortOrder;
        menu.IsExternal = model.IsExternal;
        menu.Target = model.Target;
        menu.Icon = model.Icon;
        menu.IsVisible = model.IsVisible;
        menu.LinkType = model.LinkType;
        menu.LinkId = model.LinkId;

        _db.Updateable(menu).ExecuteCommand();

        TempData["Success"] = "菜单项已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        // 子菜单提升为顶级，避免出现孤儿节点
        _db.Updateable<NavMenu>()
           .SetColumns(m => m.ParentId == null)
           .Where(m => m.ParentId == id)
           .ExecuteCommand();
        _db.Deleteable<NavMenu>().Where(m => m.Id == id).ExecuteCommand();

        TempData["Success"] = "菜单项已删除，其子菜单已提升为顶级";
        return RedirectToAction(nameof(Index));
    }

    // ============ 辅助 ============

    /// <summary>
    /// 规整绑定信息：类型非法则回落为自定义链接；绑定的对象不存在则清空绑定；
    /// 未填标题/链接时用绑定对象补全，保证前台永远有可点的地址。
    /// </summary>
    private void NormalizeLink(NavMenu model)
    {
        model.Title = (model.Title ?? string.Empty).Trim();
        model.Url = string.IsNullOrWhiteSpace(model.Url) ? null : model.Url.Trim();
        model.LinkType = MenuLinkType.IsBindable(model.LinkType)
            ? model.LinkType
            : MenuLinkType.Url;

        if (model.LinkType == MenuLinkType.Url)
        {
            model.LinkId = null;
            return;
        }

        var (name, url) = _links.Describe(model.LinkType, model.LinkId);
        if (string.IsNullOrEmpty(url))
        {
            // 绑定的对象已被删除：静默清掉绑定，保留手填地址，不阻断保存
            model.LinkId = null;
            model.LinkType = MenuLinkType.Url;
        }
        else
        {
            if (string.IsNullOrEmpty(model.Title) && !string.IsNullOrEmpty(name))
                model.Title = name!;
            model.Url ??= url;
        }
    }

    private void LoadFormData(int? excludeId, int? selectedParentId, string? selectedTarget,
        string? selectedLinkType = null, int? selectedLinkId = null)
    {
        ViewBag.ParentList = BuildParentOptions(excludeId, selectedParentId);
        var target = string.IsNullOrWhiteSpace(selectedTarget) ? "_self" : selectedTarget;
        ViewBag.TargetList = new List<SelectListItem>
        {
            new() { Value = "_self", Text = "当前窗口（_self）", Selected = target == "_self" },
            new() { Value = "_blank", Text = "新窗口（_blank）", Selected = target == "_blank" }
        };

        var linkType = MenuLinkType.IsBindable(selectedLinkType) ? selectedLinkType : MenuLinkType.Url;
        ViewBag.LinkTypeList = MenuLinkType.Options()
            .Select(o => new SelectListItem { Value = o.Value, Text = o.Text, Selected = o.Value == linkType })
            .ToList();
        ViewBag.BindList = _links.GetBindables();
        ViewBag.CurrentLinkType = linkType;
        ViewBag.CurrentLinkId = selectedLinkId;
    }

    /// <summary>把菜单树摊平为带缩进前缀的列表</summary>
    private static List<NavMenu> FlattenForView(List<NavMenu> all)
    {
        var output = new List<NavMenu>();
        WalkForView(all, null, output, 0);
        return output;
    }

    private static void WalkForView(List<NavMenu> all, int? parentId, List<NavMenu> output, int depth)
    {
        foreach (var m in ChildrenOf(all, parentId))
        {
            m.ParentName = depth == 0 ? string.Empty : new string('　', depth - 1) + "└ ";
            output.Add(m);
            WalkForView(all, m.Id, output, depth + 1);
        }
    }

    /// <summary>父级下拉选项，排除自身及其子孙，避免出现环</summary>
    private List<SelectListItem> BuildParentOptions(int? excludeId, int? selectedId)
    {
        var all = _db.Queryable<NavMenu>().OrderBy(m => m.SortOrder).ToList();

        var excluded = new HashSet<int>();
        if (excludeId.HasValue)
        {
            excluded.Add(excludeId.Value);
            CollectDescendants(all, excludeId.Value, excluded);
        }

        var options = new List<SelectListItem>
        {
            new() { Value = "", Text = "（作为顶级菜单）", Selected = !selectedId.HasValue }
        };
        AppendOptions(all, null, options, string.Empty, excluded, selectedId);
        return options;
    }

    private static void AppendOptions(List<NavMenu> all, int? parentId, List<SelectListItem> output,
        string path, HashSet<int> excluded, int? selectedId)
    {
        foreach (var m in ChildrenOf(all, parentId))
        {
            if (excluded.Contains(m.Id)) continue;
            var label = path.Length == 0 ? m.Title : path + " / " + m.Title;
            output.Add(new SelectListItem { Value = m.Id.ToString(), Text = label, Selected = selectedId == m.Id });
            AppendOptions(all, m.Id, output, label, excluded, selectedId);
        }
    }

    private static void CollectDescendants(List<NavMenu> all, int parentId, HashSet<int> acc)
    {
        foreach (var m in ChildrenOf(all, parentId))
        {
            if (!acc.Add(m.Id)) continue;
            CollectDescendants(all, m.Id, acc);
        }
    }

    private static List<NavMenu> ChildrenOf(List<NavMenu> all, int? parentId) =>
        all.Where(x => (x.ParentId ?? 0) == (parentId ?? 0))
           .OrderBy(x => x.SortOrder)
           .ThenBy(x => x.Id)
           .ToList();
}

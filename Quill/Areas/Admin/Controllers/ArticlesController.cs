using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Quill.Models;
using Quill.Services;
using Quill.Utils;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台文章管理。作者（writer）仅能管理自己创建的文章。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanWrite")]
public class ArticlesController : Controller
{
    private const int PageSize = 15;

    private readonly ArticleService _articles;
    private readonly TaxonomyService _taxonomy;
    private readonly UserService _users;
    private readonly FileService _files;
    private readonly FileManagerService _fm;
    private readonly MarkdownService _md;
    private readonly ISqlSugarClient _db;

    public ArticlesController(
        ArticleService articles,
        TaxonomyService taxonomy,
        UserService users,
        FileService files,
        FileManagerService fm,
        MarkdownService md,
        ISqlSugarClient db)
    {
        _articles = articles;
        _taxonomy = taxonomy;
        _users = users;
        _files = files;
        _fm = fm;
        _md = md;
        _db = db;
    }

    // ================= 列表 =================

    public IActionResult Index(int page = 1, ArticleStatus? status = null,
        int? categoryId = null, string? keyword = null)
    {
        // 作者角色只能看到自己的文章
        int? authorId = null;
        if (!IsPrivileged())
        {
            authorId = CurrentUserId();
        }

        var result = _articles.GetForAdmin(page, PageSize, status, categoryId, keyword, authorId);

        ViewBag.Keyword = keyword;
        ViewBag.Status = status.HasValue ? (int)status.Value : (int?)null;
        ViewBag.CategoryId = categoryId;

        var statusList = new List<SelectListItem>
        {
            new() { Value = "", Text = "全部状态", Selected = !status.HasValue }
        };
        statusList.Add(new SelectListItem
        {
            Value = "0", Text = "草稿", Selected = status == ArticleStatus.Draft
        });
        statusList.Add(new SelectListItem
        {
            Value = "1", Text = "已发布", Selected = status == ArticleStatus.Published
        });
        ViewBag.StatusList = statusList;

        var catList = new List<SelectListItem>
        {
            new() { Value = "", Text = "全部分类", Selected = !categoryId.HasValue }
        };
        catList.AddRange(_taxonomy.GetCategoriesWithCount(false)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = categoryId == c.Id
            }));
        ViewBag.CategoryList = catList;

        return View(result);
    }

    // ================= 新建 =================

    public IActionResult Create()
    {
        ViewBag.SelectedTagIds = new List<int>();
        LoadFormData(null, ArticleStatus.Draft);
        var model = new Article
        {
            AuthorId = CurrentUserId(),
            Status = ArticleStatus.Draft,
            AllowComment = true,
            IsPinned = false
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Article model, int[] tagIds, IFormFile? coverFile)
    {
        model.AuthorId = CurrentUserId();

        if (!ModelState.IsValid)
        {
            ViewBag.SelectedTagIds = tagIds.ToList();
            LoadFormData(model.CategoryId, model.Status);
            return View(model);
        }

        try
        {
            if (coverFile != null && coverFile.Length > 0)
            {
                var uploaded = await _files.SaveAsync(coverFile);
                // 封面是站点公开素材，登记到媒体库并标记为公开
                _fm.SaveFile(uploaded.FileName, uploaded.Key, uploaded.StoragePath, uploaded.ContentType,
                    uploaded.Size, null, CurrentUserId(), isPublic: true);
                model.CoverImage = $"/f/{uploaded.Key}";
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("CoverImage", ex.Message);
            ViewBag.SelectedTagIds = tagIds.ToList();
            LoadFormData(model.CategoryId, model.Status);
            return View(model);
        }

        _articles.Save(model, tagIds.ToList());
        TempData["Success"] = "文章已保存";
        return RedirectToAction(nameof(Index));
    }

    // ================= 编辑 =================

    public IActionResult Edit(int id)
    {
        var article = _articles.GetForEdit(id);
        if (article == null) return NotFound();
        if (!IsPrivileged() && article.AuthorId != CurrentUserId()) return Forbid();

        ViewBag.SelectedTagIds = _articles.GetTagIds(id);
        LoadFormData(article.CategoryId, article.Status);
        return View(article);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Article model, int[] tagIds, IFormFile? coverFile)
    {
        var article = _articles.GetForEdit(model.Id);
        if (article == null) return NotFound();
        if (!IsPrivileged() && article.AuthorId != CurrentUserId()) return Forbid();

        if (!ModelState.IsValid)
        {
            ViewBag.SelectedTagIds = tagIds.ToList();
            LoadFormData(model.CategoryId, model.Status);
            return View(model);
        }

        article.Title = model.Title;
        article.Slug = model.Slug;
        article.ContentMarkdown = model.ContentMarkdown;
        article.Summary = model.Summary;
        article.CategoryId = model.CategoryId;
        article.Status = model.Status;
        article.IsPinned = model.IsPinned;
        article.AllowComment = model.AllowComment;

        try
        {
            if (coverFile != null && coverFile.Length > 0)
            {
                var uploaded = await _files.SaveAsync(coverFile);
                _fm.SaveFile(uploaded.FileName, uploaded.Key, uploaded.StoragePath, uploaded.ContentType,
                    uploaded.Size, null, CurrentUserId(), isPublic: true);
                article.CoverImage = $"/f/{uploaded.Key}";
            }
            else
            {
                article.CoverImage = model.CoverImage;
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("CoverImage", ex.Message);
            ViewBag.SelectedTagIds = tagIds.ToList();
            LoadFormData(model.CategoryId, model.Status);
            return View(model);
        }

        _articles.Save(article, tagIds.ToList());
        TempData["Success"] = "文章已保存";
        return RedirectToAction(nameof(Index));
    }

    // ================= 其他操作 =================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var article = _articles.GetForEdit(id);
        if (article == null) return NotFound();
        if (!IsPrivileged() && article.AuthorId != CurrentUserId()) return Forbid();

        _articles.Delete(id);
        TempData["Success"] = "文章已删除";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TogglePin(int id)
    {
        var article = _articles.GetForEdit(id);
        if (article == null) return NotFound();
        if (!IsPrivileged() && article.AuthorId != CurrentUserId()) return Forbid();

        article.IsPinned = !article.IsPinned;
        _db.Updateable(article).ExecuteCommand();

        TempData["Success"] = article.IsPinned ? "已置顶" : "已取消置顶";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Markdown 实时预览（供编辑页 JS 调用）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PreviewMarkdown(string markdown)
    {
        return Content(_md.ToHtml(markdown), "text/html");
    }

    // ================= 辅助 =================

    /// <summary>admin / editor 可管理全部文章</summary>
    private bool IsPrivileged() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor);

    private int CurrentUserId()
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return 0;
        return _users.GetByUserName(name)?.Id ?? 0;
    }

    /// <summary>装载表单所需的分类、标签、状态下拉数据</summary>
    private void LoadFormData(int? selectedCategoryId, ArticleStatus selectedStatus = ArticleStatus.Draft)
    {
        var catList = new List<SelectListItem>
        {
            new() { Value = "", Text = "（未分类）", Selected = !selectedCategoryId.HasValue }
        };
        catList.AddRange(_taxonomy.GetCategoriesWithCount(false)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name,
                Selected = selectedCategoryId == c.Id
            }));
        ViewBag.CategoryList = catList;

        ViewBag.Tags = _taxonomy.GetTagsWithCount(false);

        var statusValue = ((int)selectedStatus).ToString();
        ViewBag.StatusList = new List<SelectListItem>
        {
            new() { Value = "0", Text = "草稿", Selected = statusValue == "0" },
            new() { Value = "1", Text = "已发布", Selected = statusValue == "1" }
        };
    }
}

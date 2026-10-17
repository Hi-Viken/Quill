using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Models.ViewModels;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 前台分享访问：/share/{token}
/// 无需登录即可查看/下载被分享的文件或文件夹。
/// </summary>
public class ShareController : Controller
{
    private readonly FileManagerService _fm;
    private readonly SettingsService _settings;
    private readonly FileService _files;

    public ShareController(FileManagerService fm, SettingsService settings, FileService files)
    {
        _fm = fm;
        _settings = settings;
        _files = files;
    }

    [HttpGet]
    public IActionResult Details(string token) => Render(token, null);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Details(string token, string? password) => Render(token, password);

    private IActionResult Render(string token, string? password)
    {
        var share = _fm.GetShareByToken(token);
        if (share == null)
        {
            ViewData["Title"] = "分享不存在";
            return View("Invalid", "该分享链接不存在或已被删除。");
        }

        if (!share.IsEnabled || share.IsExpired)
        {
            ViewData["Title"] = "分享已失效";
            return View("Invalid", share.IsExpired ? "该分享链接已过期。" : "该分享链接已被停用。");
        }

        if (!string.IsNullOrEmpty(share.Password) && share.Password != password)
        {
            ViewData["Title"] = "需要访问密码";
            return View(new ShareViewModel { Share = share, NeedPassword = true });
        }

        _fm.IncrementVisit(share.Id);

        var vm = new ShareViewModel { Share = share };

        if (share.MediaId.HasValue)
        {
            vm.File = _fm.GetFile(share.MediaId.Value);
            if (vm.File == null)
            {
                ViewData["Title"] = "分享不存在";
                return View("Invalid", "被分享的文件已被删除。");
            }
        }
        else if (share.FolderId.HasValue)
        {
            vm.FolderName = _fm.GetFolder(share.FolderId.Value)?.Name ?? "文件夹";
            vm.Files = _fm.GetFilesInFolderRecursive(share.FolderId.Value);
        }

        ViewData["Title"] = vm.File?.FileName ?? vm.FolderName ?? "分享";
        ViewData["SiteName"] = _settings.Get().SiteName;
        return View(vm);
    }

    /// <summary>下载：/share/{token}/download 或 /share/{token}/download/{id}</summary>
    [HttpGet]
    public IActionResult Download(string token, int? id = null, string? password = null)
    {
        var share = _fm.GetShareByToken(token);
        if (share == null || !share.IsValid) return NotFound();
        if (!string.IsNullOrEmpty(share.Password) && share.Password != password)
            return RedirectToAction(nameof(Details), new { token });

        Media? file;
        if (share.MediaId.HasValue)
        {
            if (id.HasValue && id.Value != share.MediaId.Value) return NotFound();
            file = _fm.GetFile(share.MediaId.Value);
        }
        else if (share.FolderId.HasValue && id.HasValue)
        {
            var ids = _fm.GetDescendantIds(share.FolderId.Value);
            file = _fm.GetFile(id.Value);
            if (file == null || file.FolderId == null || !ids.Contains(file.FolderId.Value)) return NotFound();
        }
        else
        {
            return NotFound();
        }

        if (file == null) return NotFound();

        string physical;
        try
        {
            physical = _files.GetPhysicalPath(file.StoragePath);
        }
        catch
        {
            return NotFound();
        }

        if (!System.IO.File.Exists(physical)) return NotFound();

        return PhysicalFile(physical, _files.ContentTypeFor(file.FileName, file.ContentType), file.FileName,
            enableRangeProcessing: true);
    }
}

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Areas.Admin.ViewModels;
using Quill.Models;
using Quill.Services;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台文件管理：资源管理器式浏览、分片上传、移动、删除与分享链接。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "CanWrite")]
public class FilesController : Controller
{
    /// <summary>每页文件数可选值</summary>
    private static readonly int[] PageSizes = { 10, 20,50, 100 };

    /// <summary>同一文件的分片串行写入锁</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ChunkLocks = new();

    private readonly FileManagerService _fm;
    private readonly FileService _files;
    private readonly UserService _users;
    private readonly MediaMarkdownService _mdExport;

    public FilesController(FileManagerService fm, FileService files, UserService users, MediaMarkdownService mdExport)
    {
        _fm = fm;
        _files = files;
        _users = users;
        _mdExport = mdExport;
    }

    // ================= 浏览 =================

    public IActionResult Index(int? folderId = null, int page = 1, int pageSize = 20, string? keyword = null,
        string sort = "name", string dir = "asc")
    {
        return View(BuildVm(folderId, page, pageSize, keyword, sort, dir));
    }

    // ================= 文件夹 =================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateFolder(int? parentId, string name, string? description)
    {
        try
        {
            _fm.CreateFolder(name, parentId, description, CurrentUserId());
            TempData["Success"] = "文件夹已创建";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { folderId = parentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RenameFolder(int id, string name, string? description, int sortOrder = 0)
    {
        var folder = _fm.GetFolder(id);
        try
        {
            _fm.UpdateFolder(id, name, description, sortOrder);
            TempData["Success"] = "文件夹已重命名";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { folderId = folder?.ParentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MoveFolder(int id, int? targetId)
    {
        var folder = _fm.GetFolder(id);
        try
        {
            _fm.MoveFolder(id, targetId);
            TempData["Success"] = "文件夹已移动";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { folderId = folder?.ParentId });
        }
        return RedirectToAction(nameof(Index), new { folderId = targetId.HasValue && targetId.Value > 0 ? targetId : null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteFolder(int id)
    {
        var folder = _fm.GetFolder(id);
        var parent = folder?.ParentId;
        _fm.DeleteFolder(id);
        TempData["Success"] = "文件夹及其内容已删除";
        return RedirectToAction(nameof(Index), new { folderId = parent });
    }

    // ================= 文件：直传（兼容小文件 / 无 JS 场景） =================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(int? folderId, IFormFile[] files)
    {
        var me = CurrentUserId();
        var saved = 0;
        var rejected = new List<string>();

        foreach (var file in files.Where(f => f != null && f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName);
            if (!_files.IsAllowed(ext))
            {
                rejected.Add(ext);
                continue;
            }

            try
            {
                var uploaded = await _files.SaveAsync(file);
                _fm.SaveFile(uploaded.FileName, uploaded.Key, uploaded.StoragePath, uploaded.ContentType,
                    uploaded.Size, folderId, me);
                saved++;
            }
            catch (Exception ex)
            {
                rejected.Add($"{file.FileName}（{ex.Message}）");
            }
        }

        if (saved > 0) TempData["Success"] = $"已上传 {saved} 个文件";
        if (rejected.Count > 0) TempData["Error"] = $"已跳过：{string.Join("、", rejected.Distinct())}";

        return RedirectToAction(nameof(Index), new { folderId });
    }

    // ================= 文件：分片上传（大文件 + 进度条） =================

    /// <summary>开始上传：校验类型/大小，创建临时分片文件</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UploadInit([FromForm] string fileName, [FromForm] long size)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty);
        if (!_files.IsAllowed(ext))
            return Json(new { ok = false, error = $"不支持的文件类型：{(string.IsNullOrEmpty(ext) ? "未知" : ext)}" });
        if (size <= 0) return Json(new { ok = false, error = "文件内容为空" });
        if (size > FileService.MaxUploadBytes) return Json(new { ok = false, error = "单个文件不能超过 2 GB" });

        var uploadId = Guid.NewGuid().ToString("N");
        System.IO.File.WriteAllBytes(_files.GetTempPath(uploadId), Array.Empty<byte>());
        return Json(new { ok = true, uploadId, chunkSize = FileService.ChunkSize });
    }

    /// <summary>上传一个分片（顺序追加到临时文件）</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(FileService.ChunkSize * 2L)]
    public async Task<IActionResult> UploadChunk([FromForm] string uploadId, [FromForm] int index, IFormFile? chunk)
    {
        if (!Guid.TryParseExact(uploadId, "N", out _))
            return Json(new { ok = false, error = "非法的上传会话" });
        if (chunk == null || chunk.Length == 0)
            return Json(new { ok = false, error = "分片内容为空" });

        var temp = _files.GetTempPath(uploadId);
        if (!System.IO.File.Exists(temp))
            return Json(new { ok = false, error = "上传会话已失效，请重新上传" });

        var gate = ChunkLocks.GetOrAdd(uploadId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            await using var fs = new FileStream(temp, FileMode.Append, FileAccess.Write, FileShare.None, 81920, true);
            await chunk.CopyToAsync(fs);
            return Json(new { ok = true, received = fs.Length });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>合并完成：校验完整性、移入私有存储、写入数据库</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UploadComplete([FromForm] string uploadId, [FromForm] string fileName,
        [FromForm] long size, [FromForm] int totalChunks, [FromForm] int? folderId, [FromForm] string? contentType)
    {
        if (!Guid.TryParseExact(uploadId, "N", out _))
            return Json(new { ok = false, error = "非法的上传会话" });

        var temp = _files.GetTempPath(uploadId);
        if (!System.IO.File.Exists(temp))
            return Json(new { ok = false, error = "上传会话已失效，请重新上传" });

        try
        {
            var actual = new FileInfo(temp).Length;
            if (size > 0 && actual != size)
                return Json(new { ok = false, error = $"文件不完整（{actual}/{size} 字节），请重试" });
            if (actual <= 0)
                return Json(new { ok = false, error = "文件内容为空" });

            var file = _files.CommitTempFile(temp, fileName, contentType);
            var id = _fm.SaveFile(file.FileName, file.Key, file.StoragePath, file.ContentType, file.Size,
                folderId, CurrentUserId());
            ChunkLocks.TryRemove(uploadId, out _);

            return Json(new
            {
                ok = true,
                id,
                key = file.Key,
                name = file.FileName,
                size = file.Size,
                chunks = totalChunks,
                accessUrl = $"/f/{file.Key}",
                downloadUrl = $"/f/{file.Key}?download=1"
            });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }

    /// <summary>取消上传，删除临时分片</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UploadAbort([FromForm] string uploadId)
    {
        if (Guid.TryParseExact(uploadId, "N", out _))
        {
            var temp = _files.GetTempPath(uploadId);
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            ChunkLocks.TryRemove(uploadId, out _);
        }
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MoveSelected(int[] ids, int? targetId, int? folderId)
    {
        var list = ids ?? Array.Empty<int>();
        _fm.MoveFiles(list, targetId);
        TempData["Success"] = $"已移动 {list.Length} 个文件";
        return RedirectToAction(nameof(Index), new { folderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteSelected(int[] ids, int? folderId)
    {
        var list = ids ?? Array.Empty<int>();
        foreach (var id in list) _fm.DeleteFile(id);
        TempData["Success"] = $"已删除 {list.Length} 个文件";
        return RedirectToAction(nameof(Index), new { folderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteFile(int id, int? folderId)
    {
        _fm.DeleteFile(id);
        TempData["Success"] = "文件已删除";
        return RedirectToAction(nameof(Index), new { folderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RenameFile(int id, string fileName, string? alt, int? folderId)
    {
        _fm.RenameFile(id, fileName, alt);
        TempData["Success"] = "文件信息已更新";
        return RedirectToAction(nameof(Index), new { folderId });
    }

    // ================= 分享 =================

    public IActionResult Shares()
    {
        var vm = new ShareListViewModel
        {
            Shares = _fm.GetShares(),
            BaseUrl = $"{Request.Scheme}://{Request.Host}"
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateShare(int? mediaId, int? folderId, int expireDays = 0, string? password = null,
        int? currentFolderId = null)
    {
        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);

        try
        {
            // 0 代表“无”，表单用隐藏域传递
            mediaId = mediaId.HasValue && mediaId.Value > 0 ? mediaId : null;
            folderId = folderId.HasValue && folderId.Value > 0 ? folderId : null;

            var share = _fm.CreateShare(mediaId, folderId, expireDays, password, CurrentUserId());
            var url = $"{Request.Scheme}://{Request.Host}/share/{share.Token}";

            if (isAjax)
                return Json(new { ok = true, url, token = share.Token, id = share.Id });

            TempData["Success"] = $"分享链接已生成：{url}";
        }
        catch (Exception ex)
        {
            if (isAjax) return Json(new { ok = false, error = ex.Message });
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { folderId = currentFolderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleShare(int id, bool enabled)
    {
        _fm.ToggleShare(id, enabled);
        TempData["Success"] = enabled ? "分享已启用" : "分享已停用";
        return RedirectToAction(nameof(Shares));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteShare(int id)
    {
        _fm.DeleteShare(id);
        TempData["Success"] = "分享已删除";
        return RedirectToAction(nameof(Shares));
    }

    // ================= 生成 Markdown =================

    /// <summary>
    /// 把选中的文件（文件夹会自动展开为其中所有文件）生成 Markdown 片段，
    /// 用于复制粘贴到文章正文。私有文件默认自动补分享链接，保证读者可访问。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Markdown([FromForm] int[]? fileIds, [FromForm] int[]? folderIds,
        [FromForm] string? style, [FromForm] bool ensureAccess = true, [FromForm] bool includeMeta = true)
    {
        var files = new List<Media>();
        var seen = new HashSet<int>();

        foreach (var id in fileIds ?? Array.Empty<int>())
        {
            if (!seen.Add(id)) continue;
            var file = _fm.GetFile(id);
            if (file != null) files.Add(file);
        }

        foreach (var folderId in (folderIds ?? Array.Empty<int>()).Distinct())
        {
            foreach (var file in _fm.GetFilesInFolderRecursive(folderId))
                if (seen.Add(file.Id)) files.Add(file);
        }

        if (files.Count == 0)
            return Json(new
            {
                ok = false,
                error = (folderIds?.Length ?? 0) > 0 ? "所选文件夹内没有文件" : "请先选择文件或文件夹"
            });

        // 单次生成上限，避免误选整个大目录时页面卡住
        var truncated = files.Count > 500;
        if (truncated)
            files = files.Take(500).ToList();

        var result = _mdExport.Build(files, new MediaMarkdownOptions
        {
            Style = style ?? MediaMarkdownService.StyleAuto,
            EnsureAccess = ensureAccess,
            IncludeMeta = includeMeta
        }, CurrentUserId());

        if (truncated)
            result.Notes.Insert(0, "一次最多生成 500 个文件，已截取前 500 个");

        return Json(new
        {
            ok = true,
            markdown = result.Markdown,
            count = result.FileCount,
            shared = result.SharedCount,
            notes = result.Notes,
            tokens = result.Tokens.Select(t => new { id = t.Key, token = t.Value })
        });
    }

    // ================= 私有辅助 =================

    private FileManagerViewModel BuildVm(int? folderId, int page, int pageSize, string? keyword, string sort, string dir)
    {
        page = page < 1 ? 1 : page;
        pageSize = Array.IndexOf(PageSizes, pageSize) >= 0 ? pageSize : PageSizes[1];

        var fid = folderId.HasValue && folderId.Value > 0 ? folderId : null;
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();

        var sortKey = (sort ?? "name").ToLowerInvariant();
        if (sortKey is not ("name" or "date" or "type" or "size")) sortKey = "name";
        sort = sortKey;
        dir = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

        var files = kw == null
            ? _fm.GetFiles(fid, page, pageSize, null, sort, dir)
            : _fm.SearchFiles(kw, page, pageSize, sort, dir);

        _fm.LoadShareTokens(files.Items);

        var subFolders = kw == null ? _fm.GetSubFolders(fid) : new List<MediaFolder>();

        return new FileManagerViewModel
        {
            FolderId = fid,
            CurrentFolder = fid.HasValue ? _fm.GetFolder(fid.Value) : null,
            Breadcrumb = _fm.GetBreadcrumb(fid),
            FolderTree = _fm.GetFolderTree(),
            SubFolders = subFolders,
            FolderShareTokens = _fm.GetFolderShareTokens(subFolders.Select(f => f.Id)),
            Files = files,
            Keyword = kw,
            Sort = sort,
            Dir = dir,
            PageSize = pageSize,
            TotalSize = _fm.GetFolderTotalSize(fid, kw),
            FolderOptions = BuildOptions(null)
        };
    }

    private List<FolderOption> BuildOptions(int? excludeId)
    {
        var options = new List<FolderOption> { new() { Id = 0, Name = "（根目录）" } };
        var skip = excludeId.HasValue ? _fm.GetDescendantIds(excludeId.Value) : new List<int>();
        Walk(_fm.GetFolderTree(), 0, options, skip);
        return options;
    }

    private static void Walk(List<MediaFolder> folders, int depth, List<FolderOption> options, List<int> skip)
    {
        foreach (var f in folders)
        {
            if (skip.Contains(f.Id)) continue;
            options.Add(new FolderOption
            {
                Id = f.Id,
                Name = depth == 0 ? f.Name : new string('　', depth) + "└ " + f.Name
            });
            Walk(f.Children, depth + 1, options, skip);
        }
    }

    private int CurrentUserId()
    {
        var name = User.Identity?.Name;
        if (string.IsNullOrEmpty(name)) return 0;
        return _users.GetByUserName(name)?.Id ?? 0;
    }
}

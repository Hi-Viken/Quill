using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 受控文件访问：/f/{key}
///
/// 文件默认不可直接访问（物理文件在 wwwroot 之外）。
/// 允许访问的两种情况：
///   1. 后台登录用户（admin / editor / writer）；
///   2. 访问者持有覆盖该文件的有效分享链接，例如 /f/{key}?share={token}，
///      带密码的分享还需 &amp;password=xxx。
/// 其它请求一律 404，不暴露文件是否存在。
/// </summary>
public class MediaController : Controller
{
    private readonly FileManagerService _fm;
    private readonly FileService _files;

    public MediaController(FileManagerService fm, FileService files)
    {
        _fm = fm;
        _files = files;
    }

    [HttpGet]
    [Route("f/{key}")]
    public IActionResult Index(string key, string? share = null, string? password = null, bool download = false)
    {
        var file = _fm.GetFileByKey(key);
        if (file == null) return NotFound();

        var isStaff = User.Identity?.IsAuthenticated == true &&
                      (User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor) || User.IsInRole(Roles.Writer));

        if (!isStaff && !file.IsPublic)
        {
            var s = _fm.GetShareByToken(share);
            if (s == null || !s.IsValid) return NotFound();
            if (!string.IsNullOrEmpty(s.Password) && !string.Equals(s.Password, password, StringComparison.Ordinal))
                return NotFound();
            if (!_fm.ShareCoversFile(s, file)) return NotFound();
        }

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

        var contentType = _files.ContentTypeFor(file.FileName, file.ContentType);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (contentType.StartsWith("image/svg", StringComparison.OrdinalIgnoreCase))
            Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        Response.Headers["Cache-Control"] = isStaff ? "private, max-age=600" : "private, no-store";

        return PhysicalFile(physical, contentType, download ? file.FileName : null, enableRangeProcessing: true);
    }
}

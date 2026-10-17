using Quill.Models;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 文件存储服务。
///
/// 文件保存在 wwwroot 之外的 App_Data/media（私有目录），
/// 磁盘文件名 = 随机 FileKey + 扩展名，原始文件名只记录在数据库中。
/// 对外统一通过 /f/{FileKey} 访问，由 MediaController 做权限校验。
/// </summary>
public class FileService
{
    private static readonly string[] AllowedExt =
    {
        // 图片
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico", ".avif",
        // 文档
        ".pdf", ".txt", ".md", ".csv", ".json", ".xml", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        // 压缩包
        ".zip", ".rar", ".7z",
        // 音视频
        ".mp4", ".webm", ".mov", ".mp3", ".wav", ".flac", ".ogg"
    };

    private static readonly Dictionary<string, string> MimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".avif"] = "image/avif",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain; charset=utf-8",
        [".md"] = "text/markdown; charset=utf-8",
        [".csv"] = "text/csv; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
        [".7z"] = "application/x-7z-compressed",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mov"] = "video/quicktime",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".flac"] = "audio/flac",
        [".ogg"] = "audio/ogg"
    };

    private readonly IWebHostEnvironment _env;

    public FileService(IWebHostEnvironment env)
    {
        _env = env;
        StorageRoot = Path.Combine(env.ContentRootPath, "App_Data", "media");
        TempRoot = Path.Combine(env.ContentRootPath, "App_Data", "temp");
        Directory.CreateDirectory(StorageRoot);
        Directory.CreateDirectory(TempRoot);
    }

    /// <summary>私有存储根目录（不在 wwwroot 下）</summary>
    public string StorageRoot { get; }

    /// <summary>分片上传临时目录</summary>
    public string TempRoot { get; }

    /// <summary>单文件大小上限：2 GB</summary>
    public const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>分片大小：8 MB</summary>
    public const int ChunkSize = 8 * 1024 * 1024;

    public bool IsAllowed(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return AllowedExt.Contains(ext.ToLowerInvariant());
    }

    /// <summary>生成 40 位随机 Key（不含原始文件名信息）</summary>
    public static string NewKey() =>
        Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8];

    public string ContentTypeFor(string fileName, string? stored = null)
    {
        if (!string.IsNullOrWhiteSpace(stored) && stored != "application/octet-stream")
            return stored!;
        var ext = Path.GetExtension(fileName);
        return MimeMap.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }

    /// <summary>保存上传文件（小文件直传）</summary>
    public async Task<(string Key, string StoragePath, string FileName, long Size, string ContentType)> SaveAsync(IFormFile file)
    {
        if (file.Length <= 0) throw new InvalidOperationException("文件内容为空");
        if (file.Length > MaxUploadBytes) throw new InvalidOperationException("单个文件不能超过 2 GB");

        var fileName = SanitizeName(file.FileName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!IsAllowed(ext)) throw new InvalidOperationException($"不允许上传 {ext} 类型的文件");

        var key = NewKey();
        var rel = BuildRelativePath(key, ext);
        var physical = GetPhysicalPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);

        await using (var stream = new FileStream(physical, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            await file.CopyToAsync(stream);
        }

        return (key, rel, fileName, file.Length, ContentTypeFor(fileName, file.ContentType));
    }

    /// <summary>把分片合并后的临时文件转正，返回入库所需信息</summary>
    public (string Key, string StoragePath, string FileName, long Size, string ContentType) CommitTempFile(
        string tempPath, string originalName, string? contentType)
    {
        var fileName = SanitizeName(originalName);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!IsAllowed(ext)) throw new InvalidOperationException($"不允许上传 {ext} 类型的文件");

        var info = new FileInfo(tempPath);
        if (!info.Exists) throw new InvalidOperationException("上传的临时文件不存在或已过期");
        if (info.Length <= 0) throw new InvalidOperationException("文件内容为空");
        if (info.Length > MaxUploadBytes) throw new InvalidOperationException("单个文件不能超过 2 GB");

        var key = NewKey();
        var rel = BuildRelativePath(key, ext);
        var physical = GetPhysicalPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        File.Move(tempPath, physical, overwrite: false);

        return (key, rel, fileName, info.Length, ContentTypeFor(fileName, contentType));
    }

    /// <summary>相对于 App_Data/media 的路径</summary>
    private static string BuildRelativePath(string key, string ext) =>
        $"{DateTime.Now:yyyyMM}/{key}{ext}";

    /// <summary>物理路径（含目录穿越防护）</summary>
    public string GetPhysicalPath(string? storagePath)
    {
        var rel = (storagePath ?? string.Empty).TrimStart('/', '\\')
                                               .Replace('/', Path.DirectorySeparatorChar)
                                               .Replace('\\', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(StorageRoot, rel));
        var root = Path.GetFullPath(StorageRoot) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("非法的文件路径");
        return full;
    }

    /// <summary>临时分片文件路径（uploadId 必须是 32 位 Guid）</summary>
    public string GetTempPath(string uploadId) => Path.Combine(TempRoot, uploadId + ".part");

    /// <summary>删除物理文件</summary>
    public void Delete(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return;
        try
        {
            var path = GetPhysicalPath(storagePath);
            if (File.Exists(path)) File.Delete(path);

            var dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                var parent = Path.GetDirectoryName(dir);
                // 只清理月份目录，不删除存储根
                if (parent != null && Path.GetFullPath(parent) == Path.GetFullPath(StorageRoot))
                    Directory.Delete(dir);
            }
        }
        catch
        {
            // 删除物理文件失败不应阻塞数据库清理
        }
    }

    /// <summary>清理超过 24 小时的未完成分片</summary>
    public void CleanTempFiles()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(TempRoot, "*.part"))
            {
                var info = new FileInfo(f);
                if (info.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-24))
                    info.Delete();
            }
        }
        catch { }
    }

    private static string SanitizeName(string? name)
    {
        var fn = Path.GetFileName(name ?? string.Empty).Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) fn = fn.Replace(c, '_');
        if (string.IsNullOrWhiteSpace(fn)) fn = "file";
        return fn.Length > 200 ? fn[^200..] : fn;
    }

    // ================= 启动时迁移 =================

    /// <summary>
    /// 初始化存储：
    /// 1. 把历史上放在 wwwroot/uploads 的文件搬到私有目录；
    /// 2. 为没有 Key 的文件补齐随机 Key；
    /// 3. 清理过期临时分片。
    /// </summary>
    public void Initialize(ISqlSugarClient db)
    {
        CleanTempFiles();

        var list = db.Queryable<Media>().ToList();
        foreach (var m in list)
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(m.FileKey))
            {
                m.FileKey = NewKey();
                changed = true;
            }

            var rel = m.StoragePath ?? string.Empty;
            if (rel.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var oldRel = rel.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var oldPhysical = Path.Combine(_env.WebRootPath, oldRel);
                if (File.Exists(oldPhysical))
                {
                    var ext = Path.GetExtension(m.FileName).ToLowerInvariant();
                    if (!IsAllowed(ext)) ext = Path.GetExtension(oldPhysical).ToLowerInvariant();
                    var newRel = BuildRelativePath(m.FileKey!, ext);
                    var newPhysical = GetPhysicalPath(newRel);
                    Directory.CreateDirectory(Path.GetDirectoryName(newPhysical)!);
                    try
                    {
                        File.Move(oldPhysical, newPhysical);
                        m.StoragePath = newRel;
                        changed = true;
                    }
                    catch
                    {
                        // 搬不动就保留原路径（仍会被 /uploads 拦截，需人工处理）
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(m.ContentType) || m.ContentType == "application/octet-stream")
            {
                m.ContentType = ContentTypeFor(m.FileName, m.ContentType);
                changed = true;
            }

            if (changed) db.Updateable(m).ExecuteCommand();
        }
    }
}

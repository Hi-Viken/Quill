using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文件管理：文件记录（上传的图片/附件），归属于某个文件夹。
/// FolderId 为 null 表示位于根目录。
///
/// 安全模型：
/// - 物理文件存放在 wwwroot 之外的 App_Data/media，无法通过静态路径直接访问；
/// - 每个文件有一个随机 FileKey，对外访问路径为 /f/{FileKey}；
/// - 只有后台登录用户，或持有有效分享链接（Token）的访客才能取到文件。
/// </summary>
[SugarTable("media")]
public class Media
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 200, IsNullable = false)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>随机访问 Key，出现在 /f/{key} 中，不可猜测</summary>
    [SugarColumn(Length = 40, IsNullable = true)]
    public string? FileKey { get; set; }

    /// <summary>
    /// 私有存储相对路径（相对于 App_Data/media），例如 202610/ab12.....jpg。
    /// 数据库列名沿用历史的 Url，避免破坏已有数据。
    /// </summary>
    [SugarColumn(ColumnName = "Url", Length = 400, IsNullable = false)]
    public string StoragePath { get; set; } = string.Empty;

    [SugarColumn(Length = 120, IsNullable = true)]
    public string? ContentType { get; set; }

    public long Size { get; set; } = 0;

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Alt { get; set; }

    /// <summary>所属文件夹 Id，null 表示根目录</summary>
    [SugarColumn(IsNullable = true)]
    public int? FolderId { get; set; }

    public int UploadedById { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 是否公开可访问。默认 false —— 只有分享链接或后台登录可访问；
    /// 文章封面等公开素材会被标记为 true。
    /// </summary>
    public bool IsPublic { get; set; } = false;

    // ---- 辅助字段 ----

    /// <summary>对外访问地址（受权限保护）</summary>
    [SugarColumn(IsIgnore = true)]
    public string AccessUrl => string.IsNullOrEmpty(FileKey) ? string.Empty : $"/f/{FileKey}";

    /// <summary>下载地址</summary>
    [SugarColumn(IsIgnore = true)]
    public string DownloadUrl => string.IsNullOrEmpty(FileKey) ? string.Empty : $"/f/{FileKey}?download=1";

    [SugarColumn(IsIgnore = true)]
    public string SizeText => Size >= 1024L * 1024 * 1024
        ? $"{Size / 1024.0 / 1024.0 / 1024.0:F2} GB"
        : Size >= 1024 * 1024
            ? $"{Size / 1024.0 / 1024.0:F1} MB"
            : Size >= 1024
                ? $"{Size / 1024.0:F0} KB"
                : $"{Size} B";

    [SugarColumn(IsIgnore = true)]
    public string Extension =>
        (Path.GetExtension(FileName) ?? string.Empty).TrimStart('.').ToLowerInvariant();

    /// <summary>是否图片（用于列表缩略图预览）</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsImage => (ContentType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                           || ImageExtensions.Contains(Extension);

    private static readonly string[] ImageExtensions =
        { "jpg", "jpeg", "png", "gif", "webp", "bmp", "svg", "ico", "avif" };

    /// <summary>文件大类，用于列表图标配色</summary>
    [SugarColumn(IsIgnore = true)]
    public string Kind => Extension switch
    {
        "jpg" or "jpeg" or "png" or "gif" or "webp" or "bmp" or "svg" or "ico" or "avif" => "image",
        "mp4" or "webm" or "mov" or "mkv" or "avi" => "video",
        "mp3" or "wav" or "flac" or "aac" or "ogg" => "audio",
        "pdf" => "pdf",
        "zip" or "rar" or "7z" or "tar" or "gz" => "archive",
        "doc" or "docx" => "doc",
        "xls" or "xlsx" or "csv" => "xls",
        "ppt" or "pptx" => "ppt",
        "txt" or "md" or "json" or "xml" or "yml" or "yaml" => "text",
        _ => "file"
    };

    /// <summary>列表展示的类型名</summary>
    [SugarColumn(IsIgnore = true)]
    public string TypeText => Kind switch
    {
        "image" => $"{Extension.ToUpperInvariant()} 图片",
        "video" => $"{Extension.ToUpperInvariant()} 视频",
        "audio" => $"{Extension.ToUpperInvariant()} 音频",
        "pdf" => "PDF 文档",
        "archive" => $"{Extension.ToUpperInvariant()} 压缩包",
        "doc" => "Word 文档",
        "xls" => "Excel 表格",
        "ppt" => "PPT 演示文稿",
        "text" => "文本文档",
        _ => string.IsNullOrEmpty(Extension) ? "文件" : $"{Extension.ToUpperInvariant()} 文件"
    };

    /// <summary>对应的图标 symbol id</summary>
    [SugarColumn(IsIgnore = true)]
    public string IconId => Kind is "image" ? "i-image" : "i-file";

    /// <summary>上传者名称（列表展示用）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? UploadedByName { get; set; }

    /// <summary>该文件当前有效的分享 Token（无则为 null）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? ShareToken { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool HasShare => !string.IsNullOrEmpty(ShareToken);

    /// <summary>最近修改时间（用于列表排序展示）</summary>
    [SugarColumn(IsIgnore = true)]
    public DateTime ModifiedAt => UploadedAt;
}

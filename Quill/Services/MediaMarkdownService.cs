using System.Text;
using Quill.Models;

namespace Quill.Services;

/// <summary>生成 Markdown 时的可选项</summary>
public class MediaMarkdownOptions
{
    /// <summary>auto / image / link / table</summary>
    public string Style { get; set; } = MediaMarkdownService.StyleAuto;

    /// <summary>私有文件是否自动补分享链接（否则只有登录用户能访问）</summary>
    public bool EnsureAccess { get; set; } = true;

    /// <summary>链接后面是否附带文件大小</summary>
    public bool IncludeMeta { get; set; } = true;
}

/// <summary>生成结果</summary>
public class MediaMarkdownResult
{
    public string Markdown { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int SharedCount { get; set; }
    public List<string> Notes { get; set; } = new();

    /// <summary>本次使用到的分享 Token（文件 Id → Token），用于前端刷新“已共享”标记</summary>
    public Dictionary<int, string> Tokens { get; set; } = new();
}

/// <summary>
/// 把媒体库文件转换成可直接粘贴到文章正文的 Markdown 片段。
///
/// 文件默认是私有的，只认 /f/{key} 的后台登录态；文章读者是匿名访客，
/// 所以这里会为没有分享的私有文件自动补一条永久分享链接，
/// 生成 /f/{key}?share={token} 形式的地址，读者无需登录即可查看。
/// </summary>
public class MediaMarkdownService
{
    public const string StyleAuto = "auto";
    public const string StyleImage = "image";
    public const string StyleLink = "link";
    public const string StyleTable = "table";

    private readonly FileManagerService _fm;

    public MediaMarkdownService(FileManagerService fm)
    {
        _fm = fm;
    }

    public MediaMarkdownResult Build(IReadOnlyList<Media> files, MediaMarkdownOptions options, int userId)
    {
        var result = new MediaMarkdownResult { FileCount = files.Count };
        var style = NormalizeStyle(options.Style);
        var items = new List<(Media File, string Url)>();

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.FileKey)) continue;

            var url = "/f/" + file.FileKey;
            if (!file.IsPublic)
            {
                if (options.EnsureAccess)
                {
                    var share = _fm.GetEffectiveShare(file);
                    if (share == null)
                    {
                        share = _fm.CreateShare(file.Id, null, 0, null, userId);
                        result.SharedCount++;
                    }
                    url += "?share=" + share.Token;
                    result.Tokens[file.Id] = share.Token;
                }
                else
                {
                    result.Notes.Add($"「{file.FileName}」未共享，匿名读者访问会 404");
                }
            }

            items.Add((file, url));
        }

        result.FileCount = items.Count;
        result.Markdown = style switch
        {
            StyleImage => BuildImages(items),
            StyleLink => BuildLinks(items, options.IncludeMeta),
            StyleTable => BuildTable(items),
            _ => BuildAuto(items, options.IncludeMeta)
        };

        return result;
    }

    private static string NormalizeStyle(string? style) => (style ?? string.Empty).ToLowerInvariant() switch
    {
        StyleImage => StyleImage,
        StyleLink => StyleLink,
        StyleTable => StyleTable,
        _ => StyleAuto
    };

    /// <summary>智能：图片用图片语法，其它文件用链接</summary>
    private static string BuildAuto(List<(Media File, string Url)> items, bool includeMeta)
    {
        var blocks = items.Select(item => item.File.IsImage
            ? $"![{EscapeText(AltOf(item.File))}]({item.Url})"
            : $"[{EscapeText(item.File.FileName)}]({item.Url}){Meta(item.File, includeMeta)}");

        return string.Join("\n\n", blocks);
    }

    /// <summary>全部按图片语法输出（适合组图 / 图集）</summary>
    private static string BuildImages(List<(Media File, string Url)> items)
    {
        return string.Join("\n\n", items.Select(item =>
            $"![{EscapeText(AltOf(item.File))}]({item.Url})"));
    }

    /// <summary>纯链接，一行一个</summary>
    private static string BuildLinks(List<(Media File, string Url)> items, bool includeMeta)
    {
        var sb = new StringBuilder();
        foreach (var (file, url) in items)
        {
            sb.Append('[').Append(EscapeText(file.FileName)).Append(']')
              .Append('(').Append(url).Append(')')
              .Append(Meta(file, includeMeta))
              .Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>表格概览：文件名 / 类型 / 大小 / 链接</summary>
    private static string BuildTable(List<(Media File, string Url)> items)
    {
        var sb = new StringBuilder();
        sb.Append("| 文件 | 类型 | 大小 | 链接 |\n");
        sb.Append("| --- | --- | --- | --- |\n");
        foreach (var (file, url) in items)
        {
            sb.Append("| ").Append(EscapeCell(file.FileName))
              .Append(" | ").Append(EscapeCell(file.TypeText))
              .Append(" | ").Append(EscapeCell(file.SizeText))
              .Append(" | [打开](").Append(url).Append(") |\n");
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static string Meta(Media file, bool includeMeta) =>
        includeMeta && !file.IsImage ? $"（{file.SizeText}）" : string.Empty;

    private static string AltOf(Media file) =>
        string.IsNullOrWhiteSpace(file.Alt)
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : file.Alt!;

    /// <summary>转义链接文字中的 Markdown 特殊字符</summary>
    private static string EscapeText(string? text)
    {
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0) return "文件";
        return s.Replace("\\", "\\\\")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace("*", "\\*")
                .Replace("_", "\\_")
                .Replace("`", "\\`");
    }

    /// <summary>表格单元格：额外转义竖线</summary>
    private static string EscapeCell(string? text) =>
        EscapeText(text).Replace("|", "\\|");
}

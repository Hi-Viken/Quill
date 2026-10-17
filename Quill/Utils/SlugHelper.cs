namespace Quill.Utils;

/// <summary>
/// 友好 URL（Slug）工具
/// </summary>
public static class SlugHelper
{
    /// <summary>
    /// 将标题转为 slug。中文标题无法转写为 ASCII，返回空字符串，调用方需回退到 id 形式。
    /// </summary>
    public static string Slugify(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        var s = title.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_') sb.Append('-');
        }
        var result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return result;
    }

    /// <summary>取用户填写的 slug，若为空则回退为 "{prefix}-{id}"</summary>
    public static string Ensure(string? input, string prefix, int id)
    {
        var s = Slugify(input ?? string.Empty);
        return string.IsNullOrEmpty(s) ? $"{prefix}-{id}" : s;
    }
}

using System.Text.Json;

namespace Quill.Models;

/// <summary>
/// 首页轮播图单项。配置以 JSON 形式整体存于 <see cref="SiteSettings.HomeBannersJson"/>，
/// 数量少、不需要单独查询，用 JSON 存比建表更省事。
/// </summary>
public class HomeBanner
{
    /// <summary>图片地址，可填 /f/{key} 的分享地址，也可填外链</summary>
    public string Image { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    /// <summary>点击跳转地址，留空则该张不可点击</summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>按钮文字，留空则不显示按钮</summary>
    public string LinkText { get; set; } = "查看详情";

    public bool NewWindow { get; set; }

    public bool HasImage => !string.IsNullOrWhiteSpace(Image);

    public bool HasLink => !string.IsNullOrWhiteSpace(Link);

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>解析配置 JSON，异常或空值时返回空列表（前台自动回落到默认 Hero）</summary>
    public static List<HomeBanner> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<HomeBanner>();
        try
        {
            var list = JsonSerializer.Deserialize<List<HomeBanner>>(json, ReadOptions);
            return list?.Where(b => b != null).ToList() ?? new List<HomeBanner>();
        }
        catch
        {
            return new List<HomeBanner>();
        }
    }

    public static string Serialize(IEnumerable<HomeBanner>? items)
    {
        var list = items?.Where(b => b != null).ToList() ?? new List<HomeBanner>();
        if (list.Count == 0) return "[]";
        try
        {
            return JsonSerializer.Serialize(list);
        }
        catch
        {
            return "[]";
        }
    }
}

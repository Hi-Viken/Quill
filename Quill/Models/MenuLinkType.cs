namespace Quill.Models;

/// <summary>
/// 菜单链接类型的取值与展示名。绑定型（category/tag/article/page）会在渲染时
/// 用对象当前的 slug 解析出真实地址，避免对象改名后菜单链接失效。
/// </summary>
public static class MenuLinkType
{
    public const string Url = "url";
    public const string Category = "category";
    public const string Tag = "tag";
    public const string Article = "article";
    public const string Page = "page";

    /// <summary>可绑定对象的类型（决定到哪张表查名字与 slug）</summary>
    public static readonly string[] Bindable = { Category, Tag, Article, Page };

    public static bool IsBindable(string? type) =>
        !string.IsNullOrWhiteSpace(type) && Bindable.Contains(type);

    public static string Label(string? type) => type switch
    {
        Category => "分类",
        Tag => "标签",
        Article => "文章",
        Page => "独立页面",
        _ => "自定义链接"
    };

    /// <summary>后台下拉用的类型选项</summary>
    public static List<(string Value, string Text)> Options() => new()
    {
        (Url, "自定义链接"),
        (Category, "分类"),
        (Tag, "标签"),
        (Article, "文章"),
        (Page, "独立页面")
    };
}

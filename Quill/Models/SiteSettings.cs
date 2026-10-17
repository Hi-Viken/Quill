using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 站点设置（单条记录，Id=1）
/// </summary>
[SugarTable("site_settings")]
public class SiteSettings
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 100, IsNullable = false)]
    public string SiteName { get; set; } = "我的博客";

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? SiteDescription { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? SiteKeywords { get; set; }

    [SugarColumn(Length = 400, IsNullable = true)]
    public string? Logo { get; set; }

    public int PostsPerPage { get; set; } = 10;

    public bool AllowComments { get; set; } = true;

    /// <summary>评论是否需要后台审核后才显示</summary>
    public bool RequireCommentApproval { get; set; } = true;

    /// <summary>是否允许游客（未登录）评论</summary>
    public bool AllowVisitorComment { get; set; } = true;

    public bool AllowRegistration { get; set; } = true;

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? Theme { get; set; } = "default";

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? FooterText { get; set; }

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? CustomHeadHtml { get; set; }

    /// <summary>首页头部展示方式：default（默认文案）/ carousel（轮播图）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? HomeHeroMode { get; set; } = HomeHeroModes.Default;

    /// <summary>轮播图配置（JSON 数组，见 <see cref="HomeBanner"/>）</summary>
    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? HomeBannersJson { get; set; }

    /// <summary>文章列表样式：list（当前列表）/ grid（封面+标题，一行多个）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? ArticleListStyle { get; set; } = ArticleListStyles.List;

    /// <summary>侧栏「最新文章」样式：list（当前模式）/ card（封面+标题）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? LatestPostsStyle { get; set; } = LatestPostsStyles.List;

    /// <summary>是否开启图片水印（仅配置，实际打水印业务未实现）</summary>
    public bool EnableWatermark { get; set; }

    /// <summary>水印位置：见 <see cref="WatermarkPositions"/></summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? WatermarkPosition { get; set; } = WatermarkPositions.BottomRight;
}

/// <summary>首页头部样式取值</summary>
public static class HomeHeroModes
{
    public const string Default = "default";
    public const string Carousel = "carousel";
}

/// <summary>文章列表样式取值</summary>
public static class ArticleListStyles
{
    public const string List = "list";
    public const string Grid = "grid";
}

/// <summary>侧栏最新文章样式取值</summary>
public static class LatestPostsStyles
{
    public const string List = "list";
    public const string Card = "card";
}

/// <summary>水印位置取值</summary>
public static class WatermarkPositions
{
    public const string BottomRight = "BottomRight";
    public const string BottomLeft = "BottomLeft";
    public const string TopRight = "TopRight";
    public const string TopLeft = "TopLeft";
    public const string Center = "Center";
    public const string Tiled = "Tiled";

    public static readonly List<(string Value, string Text)> Options = new()
    {
        (BottomRight, "右下角"),
        (BottomLeft, "左下角"),
        (TopRight, "右上角"),
        (TopLeft, "左上角"),
        (Center, "居中"),
        (Tiled, "平铺"),
    };
}

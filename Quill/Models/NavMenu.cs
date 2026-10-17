using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 导航菜单项，支持无限层级（ParentId 递归），带排序与外链/新窗口配置。
/// 链接有两种来源：手填 Url（LinkType=url）或绑定站内对象（分类/标签/文章/独立页面），
/// 绑定后地址在渲染时按对象当前 slug 实时解析，改标题改 slug 都不用回来改菜单。
/// </summary>
[SugarTable("nav_menus")]
public class NavMenu
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 60, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(Length = 400, IsNullable = true)]
    public string? Url { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ParentId { get; set; }

    public int SortOrder { get; set; } = 0;

    /// <summary>是否外链</summary>
    public bool IsExternal { get; set; } = false;

    /// <summary>打开方式，如 _blank / _self</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? Target { get; set; }

    [SugarColumn(Length = 60, IsNullable = true)]
    public string? Icon { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>链接类型：url / category / tag / article / page</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? LinkType { get; set; } = MenuLinkType.Url;

    /// <summary>绑定的站内对象主键（类型由 LinkType 决定）</summary>
    [SugarColumn(IsNullable = true)]
    public int? LinkId { get; set; }

    [SugarColumn(IsIgnore = true)]
    public List<NavMenu> Children { get; set; } = new();

    /// <summary>后台列表展示用的父级菜单名</summary>
    [SugarColumn(IsIgnore = true)]
    public string? ParentName { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool HasChildren => Children.Count > 0;

    /// <summary>绑定对象当前的地址（由 MenuLinkResolver 填充，不落库）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? ResolvedUrl { get; set; }

    /// <summary>绑定对象的名称（后台展示用）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? BoundName { get; set; }

    /// <summary>绑定的对象已被删除</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsBroken { get; set; }

    /// <summary>是否绑定了站内对象</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsBound => MenuLinkType.IsBindable(LinkType) && LinkId.HasValue;

    /// <summary>链接类型的中文名</summary>
    [SugarColumn(IsIgnore = true)]
    public string LinkTypeLabel => MenuLinkType.Label(LinkType);

    /// <summary>实际渲染用的地址：绑定结果优先，绑定失效时回落到手填 Url</summary>
    [SugarColumn(IsIgnore = true)]
    public string EffectiveUrl =>
        !string.IsNullOrWhiteSpace(ResolvedUrl)
            ? ResolvedUrl!
            : (string.IsNullOrWhiteSpace(Url) ? "#" : Url!);
}

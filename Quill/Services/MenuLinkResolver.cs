using Quill.Models;
using SqlSugar;

namespace Quill.Services;

/// <summary>后台「绑定对象」下拉选项</summary>
public class MenuBindOption
{
    public string Type { get; set; } = MenuLinkType.Url;
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;

    /// <summary>该对象当前的访问地址（用于表单里的即时预览）</summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 菜单绑定项解析：把菜单上的 LinkType + LinkId 还原成真实地址与名称。
/// 前台渲染和后台列表都走这里，保证「对象改名/改 slug 后菜单自动跟随」。
/// </summary>
public class MenuLinkResolver
{
    private readonly ISqlSugarClient _db;

    public MenuLinkResolver(ISqlSugarClient db)
    {
        _db = db;
    }

    /// <summary>批量解析（按类型分表一次查询，避免 N+1）</summary>
    /// <param name="menus">待解析的菜单集合</param>
    /// <param name="includeDrafts">后台为 true（草稿也能绑定）；前台 false 只认已发布</param>
    public void Resolve(IEnumerable<NavMenu> menus, bool includeDrafts = false)
    {
        var list = menus?.ToList() ?? new List<NavMenu>();
        if (list.Count == 0) return;

        foreach (var m in list)
        {
            m.ResolvedUrl = null;
            m.BoundName = null;
            m.IsBroken = false;
        }

        var bound = list.Where(m => m.IsBound).ToList();
        if (bound.Count == 0) return;

        foreach (var group in bound.GroupBy(m => m.LinkType!))
        {
            var ids = group.Select(m => m.LinkId!.Value).Distinct().ToList();

            switch (group.Key)
            {
                case MenuLinkType.Category:
                {
                    var map = _db.Queryable<Category>()
                                 .Where(c => ids.Contains(c.Id))
                                 .ToList()
                                 .ToDictionary(c => c.Id);
                    foreach (var m in group)
                    {
                        if (!map.TryGetValue(m.LinkId!.Value, out var c)) { m.IsBroken = true; continue; }
                        m.BoundName = c.Name;
                        m.ResolvedUrl = string.IsNullOrWhiteSpace(c.Slug)
                            ? "/Category/Index"
                            : $"/category/{c.Slug}";
                    }
                    break;
                }

                case MenuLinkType.Tag:
                {
                    var map = _db.Queryable<Tag>()
                                 .Where(t => ids.Contains(t.Id))
                                 .ToList()
                                 .ToDictionary(t => t.Id);
                    foreach (var m in group)
                    {
                        if (!map.TryGetValue(m.LinkId!.Value, out var t)) { m.IsBroken = true; continue; }
                        m.BoundName = t.Name;
                        m.ResolvedUrl = string.IsNullOrWhiteSpace(t.Slug)
                            ? "/Tag/Index"
                            : $"/tag/{t.Slug}";
                    }
                    break;
                }

                case MenuLinkType.Article:
                {
                    var q = _db.Queryable<Article>().Where(a => ids.Contains(a.Id));
                    if (!includeDrafts) q = q.Where(a => a.Status == ArticleStatus.Published);
                    var map = q.ToList().ToDictionary(a => a.Id);
                    foreach (var m in group)
                    {
                        if (!map.TryGetValue(m.LinkId!.Value, out var a)) { m.IsBroken = true; continue; }
                        m.BoundName = a.Title;
                        m.ResolvedUrl = $"/Article/Details/{(string.IsNullOrWhiteSpace(a.Slug) ? a.Id.ToString() : a.Slug)}";
                    }
                    break;
                }

                case MenuLinkType.Page:
                {
                    var q = _db.Queryable<Quill.Models.Page>().Where(p => ids.Contains(p.Id));
                    if (!includeDrafts) q = q.Where(p => p.IsPublished);
                    var map = q.ToList().ToDictionary(p => p.Id);
                    foreach (var m in group)
                    {
                        if (!map.TryGetValue(m.LinkId!.Value, out var p)) { m.IsBroken = true; continue; }
                        m.BoundName = p.Title;
                        m.ResolvedUrl = string.IsNullOrWhiteSpace(p.Slug)
                            ? "/"
                            : $"/page/{p.Slug}";
                    }
                    break;
                }
            }
        }
    }

    /// <summary>可绑定的对象清单（分类按层级显示，文章只列已发布 + 最近 200 篇）</summary>
    public List<MenuBindOption> GetBindables()
    {
        var result = new List<MenuBindOption>();

        var cats = _db.Queryable<Category>().OrderBy(c => c.SortOrder).OrderBy(c => c.Id).ToList();
        AppendCategories(cats, null, string.Empty, result);

        var tags = _db.Queryable<Tag>().OrderBy(t => t.Name).ToList();
        result.AddRange(tags.Select(t => new MenuBindOption
        {
            Type = MenuLinkType.Tag,
            Id = t.Id,
            Text = t.Name,
            Group = "标签",
            Url = string.IsNullOrWhiteSpace(t.Slug) ? "/Tag/Index" : $"/tag/{t.Slug}"
        }));

        var articles = _db.Queryable<Article>()
                          .Where(a => a.Status == ArticleStatus.Published)
                          .OrderBy(a => a.Id, OrderByType.Desc)
                          .Take(200)
                          .ToList();
        result.AddRange(articles.Select(a => new MenuBindOption
        {
            Type = MenuLinkType.Article,
            Id = a.Id,
            Text = a.Title,
            Group = "文章",
            Url = $"/Article/Details/{(string.IsNullOrWhiteSpace(a.Slug) ? a.Id.ToString() : a.Slug)}"
        }));

        var pages = _db.Queryable<Quill.Models.Page>().OrderBy(p => p.SortOrder).OrderBy(p => p.Id).ToList();
        result.AddRange(pages.Select(p => new MenuBindOption
        {
            Type = MenuLinkType.Page,
            Id = p.Id,
            Text = p.Title + (p.IsPublished ? string.Empty : "（未发布）"),
            Group = "独立页面",
            Url = string.IsNullOrWhiteSpace(p.Slug) ? "/" : $"/page/{p.Slug}"
        }));

        return result;
    }

    /// <summary>取单个绑定对象的名称与地址（保存时回填标题/URL 用）</summary>
    public (string? Name, string? Url) Describe(string? type, int? id)
    {
        if (!MenuLinkType.IsBindable(type) || !id.HasValue) return (null, null);

        var probe = new NavMenu { LinkType = type, LinkId = id };
        Resolve(new[] { probe }, includeDrafts: true);
        return (probe.BoundName, probe.ResolvedUrl);
    }

    private static void AppendCategories(List<Category> all, int? parentId, string path, List<MenuBindOption> output)
    {
        foreach (var c in all.Where(x => (x.ParentId ?? 0) == (parentId ?? 0))
                             .OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
        {
            var label = path.Length == 0 ? c.Name : path + " / " + c.Name;
            output.Add(new MenuBindOption
            {
                Type = MenuLinkType.Category,
                Id = c.Id,
                Text = label,
                Group = "分类",
                Url = string.IsNullOrWhiteSpace(c.Slug) ? "/Category/Index" : $"/category/{c.Slug}"
            });
            AppendCategories(all, c.Id, label, output);
        }
    }
}

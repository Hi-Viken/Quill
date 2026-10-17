using Quill.Models;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 站点上下文服务：站点设置、多级导航菜单树、友情链接。
/// 布局页通过 @inject 直接使用。
/// </summary>
public class SettingsService
{
    private readonly ISqlSugarClient _db;
    private readonly MenuLinkResolver _links;
    private SiteSettings? _settings;

    public SettingsService(ISqlSugarClient db, MenuLinkResolver links)
    {
        _db = db;
        _links = links;
    }

    /// <summary>获取站点设置（不存在则创建默认）</summary>
    public SiteSettings Get()
    {
        if (_settings != null) return _settings;
        var s = _db.Queryable<SiteSettings>().First();
        if (s == null)
        {
            s = new SiteSettings();
            s.Id = _db.Insertable(s).ExecuteReturnIdentity();
        }
        _settings = s;
        return s;
    }

    public void Save(SiteSettings model)
    {
        model.Id = model.Id <= 0 ? 1 : model.Id;
        var exists = _db.Queryable<SiteSettings>().Any(x => x.Id == model.Id);
        if (exists) _db.Updateable(model).ExecuteCommand();
        else _db.Insertable(model).ExecuteReturnIdentity();
        _settings = null;
    }

    /// <summary>构建多级菜单树（递归，支持任意层级）</summary>
    public List<NavMenu> GetMenuTree(bool onlyVisible = true)
    {
        var query = _db.Queryable<NavMenu>();
        if (onlyVisible) query = query.Where(m => m.IsVisible);
        var all = query.OrderBy(m => m.SortOrder).ToList();

        var roots = all.Where(m => m.ParentId == null || m.ParentId == 0).ToList();
        BuildChildren(roots, all);

        // 绑定型菜单按对象当前 slug 解析地址（对象改了名/改了 slug 无需再改菜单）
        _links.Resolve(all);

        return roots;
    }

    private static void BuildChildren(List<NavMenu> parents, List<NavMenu> all)
    {
        foreach (var p in parents)
        {
            var children = all.Where(c => c.ParentId == p.Id).ToList();
            if (children.Count > 0)
            {
                BuildChildren(children, all);
                p.Children = children;
            }
        }
    }

    /// <summary>获取所有菜单项（后台管理用，平铺带层级名）</summary>
    public List<NavMenu> GetAllFlat()
    {
        var all = _db.Queryable<NavMenu>().OrderBy(m => m.SortOrder).ToList();
        var byId = all.ToDictionary(m => m.Id);
        foreach (var m in all)
        {
            m.ParentName = (m.ParentId.HasValue && byId.ContainsKey(m.ParentId.Value))
                ? byId[m.ParentId.Value].Title : null;
        }
        _links.Resolve(all, includeDrafts: true);
        return all;
    }

    public List<FriendLink> GetFriendLinks(bool onlyVisible = true)
    {
        var q = _db.Queryable<FriendLink>();
        if (onlyVisible) q = q.Where(f => f.IsVisible);
        return q.OrderBy(f => f.SortOrder).ToList();
    }
}

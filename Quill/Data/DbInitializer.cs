using Quill.Models;
using SqlSugar;

namespace Quill.Data;

/// <summary>
/// 数据库初始化：SqlSugar CodeFirst 建表 + 首次运行种子数据
/// 说明：长文本列统一使用 Models.ColumnTypes.LongText，以兼容 SQLite 与 SQL Server。
/// </summary>
public static class DbInitializer
{
    /// <summary>按实体自动建表（已存在则跳过）</summary>
    public static void EnsureTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables(
            typeof(ApplicationUser),
            typeof(Category),
            typeof(Tag),
            typeof(ArticleTag),
            typeof(Article),
            typeof(Page),
            typeof(Comment),
            typeof(Like),
            typeof(NavMenu),
            typeof(FriendLink),
            typeof(SiteSettings),
            typeof(Media),
            typeof(MediaFolder),
            typeof(MediaShare)
        );

        // 兼容旧库：早期 media 表没有 FolderId / 新表缺列时自动补齐
        EnsureMediaFolderColumn(db);
        EnsureMediaKeyColumn(db);

        // 兼容旧库：菜单绑定（LinkType / LinkId）
        EnsureNavMenuLinkColumns(db);

        // 兼容旧库：首页展示 / 列表样式等新增设置项
        EnsureSiteSettingsColumns(db);
    }

    /// <summary>为已存在的 site_settings 表补齐新增的设置列</summary>
    private static void EnsureSiteSettingsColumns(ISqlSugarClient db)
    {
        try
        {
            if (!db.DbMaintenance.IsAnyTable("site_settings")) return;

            void AddColumn(string name, string dataType, int length)
            {
                if (db.DbMaintenance.IsAnyColumn("site_settings", name)) return;
                try
                {
                    db.DbMaintenance.AddColumn("site_settings", new DbColumnInfo
                    {
                        TableName = "site_settings",
                        DbColumnName = name,
                        PropertyName = name,
                        DataType = dataType,
                        Length = length,
                        IsNullable = true,
                        IsIdentity = false
                    });
                }
                catch
                {
                    var sqlType = dataType == "text" || dataType == "varchar" ? "text" : "int";
                    try { db.Ado.ExecuteCommand($"ALTER TABLE site_settings ADD COLUMN {name} {sqlType} NULL"); } catch { }
                }
            }

            AddColumn("HomeHeroMode", "varchar", 20);
            AddColumn("HomeBannersJson", "text", 0);
            AddColumn("ArticleListStyle", "varchar", 20);
            AddColumn("LatestPostsStyle", "varchar", 20);
            AddColumn("EnableWatermark", "int", 0);
            AddColumn("WatermarkPosition", "varchar", 20);

            db.Ado.ExecuteCommand(
                "UPDATE site_settings SET HomeHeroMode = 'default' WHERE HomeHeroMode IS NULL OR HomeHeroMode = ''");
            db.Ado.ExecuteCommand(
                "UPDATE site_settings SET ArticleListStyle = 'list' WHERE ArticleListStyle IS NULL OR ArticleListStyle = ''");
            db.Ado.ExecuteCommand(
                "UPDATE site_settings SET LatestPostsStyle = 'list' WHERE LatestPostsStyle IS NULL OR LatestPostsStyle = ''");
            db.Ado.ExecuteCommand(
                "UPDATE site_settings SET WatermarkPosition = 'BottomRight' WHERE WatermarkPosition IS NULL OR WatermarkPosition = ''");
        }
        catch { }
    }

    /// <summary>为已存在的 nav_menus 表补充绑定列，并把老数据标记为「自定义链接」</summary>
    private static void EnsureNavMenuLinkColumns(ISqlSugarClient db)
    {
        void AddColumn(string name, string dataType, int length)
        {
            try
            {
                if (db.DbMaintenance.IsAnyColumn("nav_menus", name)) return;
                db.DbMaintenance.AddColumn("nav_menus", new DbColumnInfo
                {
                    TableName = "nav_menus",
                    DbColumnName = name,
                    PropertyName = name,
                    DataType = dataType,
                    Length = length,
                    IsNullable = true,
                    IsIdentity = false
                });
            }
            catch
            {
                try { db.Ado.ExecuteCommand($"ALTER TABLE nav_menus ADD COLUMN {name} " + (dataType == "string" || dataType == "varchar" ? $"varchar({length})" : "int") + " NULL"); }
                catch { }
            }
        }

        try
        {
            if (!db.DbMaintenance.IsAnyTable("nav_menus")) return;

            AddColumn("LinkType", "varchar", 20);
            AddColumn("LinkId", "int", 0);

            db.Ado.ExecuteCommand("UPDATE nav_menus SET LinkType = 'url' WHERE LinkType IS NULL OR LinkType = ''");

            UpgradeLegacyMenuLinks(db);
        }
        catch { }
    }

    /// <summary>
    /// 把早期手写的 /category/{slug}、/tag/{slug}、/page/{slug} 地址自动转成绑定，
    /// 以后分类改名或 slug 变化，菜单链接会自动跟随。
    /// </summary>
    private static void UpgradeLegacyMenuLinks(ISqlSugarClient db)
    {
        try
        {
            var menus = db.Queryable<NavMenu>()
                          .Where(m => m.LinkType == null || m.LinkType == "url")
                          .ToList();
            if (menus.Count == 0) return;

            var catMap = db.Queryable<Category>().ToList()
                            .Where(c => !string.IsNullOrWhiteSpace(c.Slug))
                            .GroupBy(c => c.Slug!.Trim().ToLowerInvariant())
                            .ToDictionary(g => g.Key, g => g.First().Id);
            var tagMap = db.Queryable<Tag>().ToList()
                            .Where(t => !string.IsNullOrWhiteSpace(t.Slug))
                            .GroupBy(t => t.Slug!.Trim().ToLowerInvariant())
                            .ToDictionary(g => g.Key, g => g.First().Id);
            var pageMap = db.Queryable<Quill.Models.Page>().ToList()
                            .Where(p => !string.IsNullOrWhiteSpace(p.Slug))
                            .GroupBy(p => p.Slug!.Trim().ToLowerInvariant())
                            .ToDictionary(g => g.Key, g => g.First().Id);

            foreach (var m in menus)
            {
                var url = (m.Url ?? string.Empty).Trim();
                if (url.Length == 0 || url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                var normalized = url.TrimStart('.', '/').TrimEnd('/');
                var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var slug = parts[^1].ToLowerInvariant();
                string type;
                Dictionary<string, int> map;

                switch (parts[0].ToLowerInvariant())
                {
                    case "category": type = MenuLinkType.Category; map = catMap; break;
                    case "tag": type = MenuLinkType.Tag; map = tagMap; break;
                    case "page": type = MenuLinkType.Page; map = pageMap; break;
                    default: continue;
                }

                if (!map.TryGetValue(slug, out var id)) continue;

                m.LinkType = type;
                m.LinkId = id;
                db.Updateable(m).UpdateColumns(x => new { x.LinkType, x.LinkId }).ExecuteCommand();
            }
        }
        catch { }
    }

    /// <summary>为已存在的 media 表补充 FileKey 列（文件私有化访问 Key）</summary>
    private static void EnsureMediaKeyColumn(ISqlSugarClient db)
    {
        try
        {
            if (!db.DbMaintenance.IsAnyTable("media")) return;
            if (db.DbMaintenance.IsAnyColumn("media", "FileKey")) return;

            db.DbMaintenance.AddColumn("media", new DbColumnInfo
            {
                TableName = "media",
                DbColumnName = "FileKey",
                PropertyName = "FileKey",
                DataType = "varchar",
                Length = 40,
                IsNullable = true,
                IsIdentity = false
            });
        }
        catch
        {
            try { db.Ado.ExecuteCommand("ALTER TABLE media ADD COLUMN FileKey varchar(40) NULL"); } catch { }
        }
    }

    /// <summary>为已存在的 media 表补充 FolderId 列（新建库无需此步骤）</summary>
    private static void EnsureMediaFolderColumn(ISqlSugarClient db)
    {
        try
        {
            if (!db.DbMaintenance.IsAnyTable("media")) return;
            if (db.DbMaintenance.IsAnyColumn("media", "FolderId")) return;

            db.DbMaintenance.AddColumn("media", new DbColumnInfo
            {
                TableName = "media",
                DbColumnName = "FolderId",
                PropertyName = "FolderId",
                DataType = "int",
                Length = 0,
                IsNullable = true,
                IsIdentity = false
            });
        }
        catch
        {
            // 兜底：某些数据库 DbMaintenance 补列失败时，直接用 DDL
            try { db.Ado.ExecuteCommand("ALTER TABLE media ADD COLUMN FolderId int NULL"); } catch { }
        }
    }

    /// <summary>种子数据（仅在表为空时写入，可重复执行）</summary>
    public static void Seed(ISqlSugarClient db, Services.UserService userService, Services.MarkdownService md)
    {
        SeedSettings(db);
        SeedUsers(db, userService);
        SeedCategories(db);
        SeedTags(db);
        SeedArticles(db, md);
        SeedPages(db, md);
        SeedNavMenus(db);
        SeedFriendLinks(db);
        SeedFolders(db);
    }

    // ---------------- 站点设置 ----------------
    private static void SeedSettings(ISqlSugarClient db)
    {
        if (db.Queryable<SiteSettings>().Any()) return;
        db.Insertable(new SiteSettings
        {
            SiteName = "Quill",
            SiteDescription = "基于 ASP.NET Core MVC + SqlSugar 的博客系统",
            SiteKeywords = "博客,ASP.NET Core,SqlSugar,Markdown",
            PostsPerPage = 10,
            AllowComments = true,
            RequireCommentApproval = true,
            AllowVisitorComment = true,
            AllowRegistration = true,
            Theme = "default",
            FooterText = "© 2026 Quill · Powered by ASP.NET Core & SqlSugar"
        }).ExecuteCommand();
    }

    // ---------------- 用户 ----------------
    private static void SeedUsers(ISqlSugarClient db, Services.UserService userService)
    {
        if (db.Queryable<ApplicationUser>().Any()) return;

        var admin = userService.Register("admin", "admin@netblog.dev", "admin123", Roles.Admin);
        admin.DisplayName = "站长";
        admin.Bio = "热爱 .NET 与嵌入式开发的站长。";
        userService.Save(admin);

        var editor = userService.Register("editor", "editor@netblog.dev", "editor123", Roles.Editor);
        editor.DisplayName = "编辑小李";
        userService.Save(editor);

        var writer = userService.Register("writer", "writer@netblog.dev", "writer123", Roles.Writer);
        writer.DisplayName = "作者小王";
        userService.Save(writer);
    }

    // ---------------- 分类 ----------------
    private static void SeedCategories(ISqlSugarClient db)
    {
        if (db.Queryable<Category>().Any()) return;
        var list = new List<Category>
        {
            new() { Name = "技术随笔", Slug = "tech", Description = ".NET / C# / 嵌入式相关", SortOrder = 1 },
            new() { Name = "生活杂记", Slug = "life", Description = "日常点滴", SortOrder = 2 },
            new() { Name = "开源项目", Slug = "opensource", Description = "项目发布与维护记录", SortOrder = 3 }
        };
        db.Insertable(list).ExecuteCommand();
    }

    // ---------------- 标签 ----------------
    private static void SeedTags(ISqlSugarClient db)
    {
        if (db.Queryable<Tag>().Any()) return;
        db.Insertable(new List<Tag>
        {
            new() { Name = "ASP.NET Core", Slug = "aspnetcore" },
            new() { Name = "SqlSugar", Slug = "sqlsugar" },
            new() { Name = "C#", Slug = "csharp" },
            new() { Name = "ESP32", Slug = "esp32" },
            new() { Name = "Markdown", Slug = "markdown" }
        }).ExecuteCommand();
    }

    // ---------------- 文章 ----------------
    private static void SeedArticles(ISqlSugarClient db, Services.MarkdownService md)
    {
        if (db.Queryable<Article>().Any()) return;

        var admin = db.Queryable<ApplicationUser>().First(u => u.UserName == "admin");
        var writer = db.Queryable<ApplicationUser>().First(u => u.UserName == "writer");
        var tech = db.Queryable<Category>().First(c => c.Slug == "tech");
        var life = db.Queryable<Category>().First(c => c.Slug == "life");
        var os = db.Queryable<Category>().First(c => c.Slug == "opensource");

        var now = DateTime.Now;

        var a1 = new Article
        {
            Title = "欢迎使用 Quill",
            Slug = "welcome-netblog",
            ContentMarkdown = """
# 欢迎使用 Quill

这是一个基于 **ASP.NET Core MVC** 与 **SqlSugar** 构建的博客系统。

## 特性一览

- 文章：Markdown 编辑、草稿 / 发布 / 置顶、封面图
- 独立页面、分类、标签、归档、作者
- 评论（含多级回复）与点赞
- 多角色：admin / editor / writer / visitor
- 后台：仪表盘、写文章、分类标签、评论审核、用户、站点设置、媒体、导航菜单（多级）、友情链接

## 代码示例

```csharp
var list = db.Queryable<Article>()
    .Where(a => a.Status == ArticleStatus.Published)
    .OrderBy(a => a.PublishedAt, OrderByType.Desc)
    .ToList();
```

> 祝你使用愉快！

| 模块 | 状态 |
| --- | --- |
| 前台 | 完成 |
| 后台 | 完成 |
""",
            Status = ArticleStatus.Published,
            IsPinned = true,
            AllowComment = true,
            AuthorId = admin.Id,
            CategoryId = os.Id,
            CreatedAt = now.AddDays(-5),
            PublishedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5)
        };

        var a2 = new Article
        {
            Title = "SqlSugar 快速上手",
            Slug = "sqlsugar-quickstart",
            ContentMarkdown = """
## 为什么选择 SqlSugar

SqlSugar 是国产 ORM，具备 **CodeFirst**、多库支持、链式查询等能力，且无需迁移工具。

### 建表

```csharp
db.CodeFirst.InitTables(typeof(Article), typeof(Category));
```

### 分页查询

```csharp
var total = 0;
var items = db.Queryable<Article>().ToPageList(page, 10, ref total);
```

### 事务

```csharp
db.Ado.UseTran(() => { /* ... */ });
```
""",
            Status = ArticleStatus.Published,
            AllowComment = true,
            AuthorId = admin.Id,
            CategoryId = tech.Id,
            CreatedAt = now.AddDays(-3),
            PublishedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-3)
        };

        var a3 = new Article
        {
            Title = "用 Markdown 写文章的乐趣",
            Slug = "markdown-joy",
            ContentMarkdown = """
Markdown 让你专注于内容本身。

- **加粗**、*斜体*、`代码`
- [链接](https://dotnet.microsoft.com/)
- 列表、引用、表格都很自然

行内代码 `dotnet run` 与代码块：

```bash
dotnet new mvc -n MyBlog
```
""",
            Status = ArticleStatus.Published,
            AllowComment = true,
            AuthorId = writer.Id,
            CategoryId = life.Id,
            CreatedAt = now.AddDays(-1),
            PublishedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1)
        };

        var a4 = new Article
        {
            Title = "（草稿）ESP32-S3 踩坑记录",
            Slug = "esp32s3-notes",
            ContentMarkdown = "这是一篇**草稿**，尚未发布，前台不可见。\n\n- USB-OTG 与 hwcdc 的区别\n- GPIO22 在部分开发板上未引出",
            Status = ArticleStatus.Draft,
            AllowComment = true,
            AuthorId = writer.Id,
            CategoryId = tech.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var a in new[] { a1, a2, a3, a4 })
        {
            a.ContentHtml = md.ToHtml(a.ContentMarkdown);
            a.Summary = md.ToSummary(a.ContentMarkdown, 120);
            a.Id = db.Insertable(a).ExecuteReturnIdentity();
        }

        // 标签关联
        var tAspNet = db.Queryable<Tag>().First(t => t.Slug == "aspnetcore");
        var tSqlSugar = db.Queryable<Tag>().First(t => t.Slug == "sqlsugar");
        var tCsharp = db.Queryable<Tag>().First(t => t.Slug == "csharp");
        var tEsp = db.Queryable<Tag>().First(t => t.Slug == "esp32");
        var tMd = db.Queryable<Tag>().First(t => t.Slug == "markdown");

        db.Insertable(new List<ArticleTag>
        {
            new() { ArticleId = a1.Id, TagId = tAspNet.Id },
            new() { ArticleId = a1.Id, TagId = tSqlSugar.Id },
            new() { ArticleId = a2.Id, TagId = tSqlSugar.Id },
            new() { ArticleId = a2.Id, TagId = tCsharp.Id },
            new() { ArticleId = a3.Id, TagId = tMd.Id },
            new() { ArticleId = a4.Id, TagId = tEsp.Id }
        }).ExecuteCommand();

        // 示例评论（已审核）
        db.Insertable(new Comment
        {
            ArticleId = a1.Id,
            AuthorName = "访客小明",
            Email = "ming@example.com",
            Content = "写得很好，收藏了！",
            IsApproved = true,
            CreatedAt = now.AddDays(-4)
        }).ExecuteCommand();

        var parent = new Comment
        {
            ArticleId = a1.Id,
            AuthorName = "访客小红",
            Content = "请问支持多数据库吗？",
            IsApproved = true,
            CreatedAt = now.AddDays(-4).AddHours(2)
        };
        parent.Id = db.Insertable(parent).ExecuteReturnIdentity();

        db.Insertable(new Comment
        {
            ArticleId = a1.Id,
            ParentId = parent.Id,
            AuthorName = "站长",
            UserId = admin.Id,
            Content = "支持，切换 DbType 与连接串即可。",
            IsApproved = true,
            CreatedAt = now.AddDays(-4).AddHours(3)
        }).ExecuteCommand();

        // 待审核评论（演示后台审核）
        db.Insertable(new Comment
        {
            ArticleId = a1.Id,
            AuthorName = "路人甲",
            Content = "这条评论等待管理员审核…",
            IsApproved = false,
            CreatedAt = now
        }).ExecuteCommand();

        // 示例点赞
        db.Insertable(new List<Like>
        {
            new() { ArticleId = a1.Id, UserId = admin.Id, CreatedAt = now.AddDays(-4) },
            new() { ArticleId = a1.Id, IpAddress = "127.0.0.1", CreatedAt = now.AddDays(-4) },
            new() { ArticleId = a2.Id, UserId = admin.Id, CreatedAt = now.AddDays(-3) }
        }).ExecuteCommand();
    }

    // ---------------- 独立页面 ----------------
    private static void SeedPages(ISqlSugarClient db, Services.MarkdownService md)
    {
        if (db.Queryable<Page>().Any()) return;
        var admin = db.Queryable<ApplicationUser>().First(u => u.UserName == "admin");

        var about = new Page
        {
            Title = "关于我",
            Slug = "about",
            ContentMarkdown = """
## 关于本站

本站由 **ASP.NET Core MVC + SqlSugar** 驱动，用于记录技术与生活。

- 后端：ASP.NET Core MVC (.NET 10)
- ORM：SqlSugar
- 编辑器：Markdown

### 联系我

邮箱：admin@netblog.dev
""",
            IsPublished = true,
            SortOrder = 1,
            AllowComment = false,
            AuthorId = admin.Id,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        about.ContentHtml = md.ToHtml(about.ContentMarkdown);
        db.Insertable(about).ExecuteCommand();
    }

    // ---------------- 多级导航菜单 ----------------
    private static void SeedNavMenus(ISqlSugarClient db)
    {
        if (db.Queryable<NavMenu>().Any()) return;

        var home = new NavMenu { Title = "首页", Url = "/", SortOrder = 1 };
        home.Id = db.Insertable(home).ExecuteReturnIdentity();

        var articles = new NavMenu { Title = "文章", Url = "/Article", SortOrder = 2 };
        articles.Id = db.Insertable(articles).ExecuteReturnIdentity();

        // 二级菜单
        db.Insertable(new List<NavMenu>
        {
            new() { Title = "全部分类", Url = "/Category", ParentId = articles.Id, SortOrder = 1 },
            new() { Title = "标签云", Url = "/Tag", ParentId = articles.Id, SortOrder = 2 },
            new() { Title = "归档", Url = "/Archive", ParentId = articles.Id, SortOrder = 3 }
        }).ExecuteCommand();

        var more = new NavMenu { Title = "更多", Url = "#", SortOrder = 3 };
        more.Id = db.Insertable(more).ExecuteReturnIdentity();

        var sub = new NavMenu { Title = "友情链接", Url = "#links", ParentId = more.Id, SortOrder = 1 };
        sub.Id = db.Insertable(sub).ExecuteReturnIdentity();

        // 三级菜单（外链、新窗口）
        db.Insertable(new NavMenu
        {
            Title = "ASP.NET 官网",
            Url = "https://dotnet.microsoft.com/",
            ParentId = sub.Id,
            SortOrder = 1,
            IsExternal = true,
            Target = "_blank"
        }).ExecuteCommand();

        db.Insertable(new NavMenu
        {
            Title = "后台管理",
            Url = "/Admin",
            ParentId = more.Id,
            SortOrder = 2
        }).ExecuteCommand();

        var about = new NavMenu { Title = "关于", Url = "/page/about", SortOrder = 4 };
        db.Insertable(about).ExecuteCommand();
    }

    // ---------------- 文件管理：示例文件夹 ----------------
    private static void SeedFolders(ISqlSugarClient db)
    {
        if (db.Queryable<MediaFolder>().Any()) return;
        var admin = db.Queryable<ApplicationUser>().First(u => u.UserName == "admin");

        var images = new MediaFolder { Name = "图片", Description = "站点图片资源", SortOrder = 1, CreatedById = admin.Id };
        images.Id = db.Insertable(images).ExecuteReturnIdentity();

        db.Insertable(new MediaFolder
        {
            Name = "封面图",
            ParentId = images.Id,
            Description = "文章封面",
            SortOrder = 1,
            CreatedById = admin.Id
        }).ExecuteCommand();

        db.Insertable(new MediaFolder
        {
            Name = "文档",
            Description = "附件与文档",
            SortOrder = 2,
            CreatedById = admin.Id
        }).ExecuteCommand();
    }

    // ---------------- 友情链接 ----------------
    private static void SeedFriendLinks(ISqlSugarClient db)
    {
        if (db.Queryable<FriendLink>().Any()) return;
        db.Insertable(new List<FriendLink>
        {
            new() { Name = "Microsoft .NET", Url = "https://dotnet.microsoft.com/", Description = ".NET 官方网站", SortOrder = 1 },
            new() { Name = "SqlSugar 文档", Url = "https://www.donet5.com/Home/Doc", Description = "国产 ORM 文档", SortOrder = 2 },
            new() { Name = "GitHub", Url = "https://github.com/", Description = "代码托管", SortOrder = 3 }
        }).ExecuteCommand();
    }
}

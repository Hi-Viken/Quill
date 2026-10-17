# Quill

**基于 ASP.NET Core MVC (.NET 10) + SqlSugar 的自托管博客 / 轻量 CMS——Markdown 原生、零迁移文件、可直接复制部署。**

[English](README.md) · 简体中文

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/)
[![ORM](https://img.shields.io/badge/ORM-SqlSugar-2F6FEB)](https://www.donet5.com/)
[![Storage](https://img.shields.io/badge/数据库-SQLite%20%7C%20SQL%20Server%20%7C%20MySQL-003B57?logo=sqlite&logoColor=white)](#切换数据库)
[![License](https://img.shields.io/badge/协议-MIT-success.svg)](LICENSE)

---

## 目录

- [为什么是 Quill](#为什么是-quill)
- [功能清单](#功能清单)
- [技术选型](#技术选型)
- [环境要求](#环境要求)
- [快速开始](#快速开始)
- [默认账号](#默认账号)
- [路由一览](#路由一览)
- [配置说明](#配置说明)
- [切换数据库](#切换数据库)
- [目录结构](#目录结构)
- [实现要点](#实现要点)
- [安全清单](#安全清单)
- [部署](#部署)
- [已知限制与规划](#已知限制与规划)
- [参与贡献](#参与贡献)
- [许可协议](#许可协议)

---

## 为什么是 Quill

市面上的 .NET 博客系统往往自带一堆你不会用到的负担：EF Core 迁移文件、一整套 Identity 表结构、主题市场。Quill 反过来做：

- **没有迁移文件。** SqlSugar CodeFirst 在启动时建表并补齐旧库结构——拷贝目录、`dotnet run`，站点就活了。
- **Markdown 优先。** 正文以 Markdown 存储，经 Markdig 渲染，且默认关闭原始 HTML，天然防 XSS。
- **无 CDN、无构建步骤。** Bootstrap 5 与 jQuery 直接内置在 `wwwroot/lib`，部署链路上没有 `npm install`。
- **一套代码，三种数据库。** 个人站点用 SQLite；正式环境改两行配置即可切到 SQL Server 或 MySQL。
- **代码面小、可通读。** 前台 11 个控制器、后台 11 个控制器，服务层薄；一个下午就能把整条请求链路读完。

---

## 功能清单

### 内容

| 功能 | 说明 |
| --- | --- |
| 文章 | Markdown 正文、草稿 / 发布、置顶、封面图、摘要、阅读数 |
| 独立页面 | 如「关于我」，独立于分类 / 标签 / 归档体系 |
| 分类 | 通过 `ParentId` 支持多级，带排序字段，独立分类归档页 |
| 标签 | 多对多关联、标签云、独立标签归档页 |
| 归档 | 按年 / 月聚合 |
| 作者 | 作者列表与各作者文章归档 |
| 搜索 | 已发布文章的标题 / 内容关键词搜索（`/Article/Search?q=`） |

### 互动

- **评论**：通过 `ParentId` 支持**无限层级回复**，前台渲染为递归评论树；游客与登录用户均可评论；后台可开关「评论需审核」，支持批量通过。
- **点赞**：登录用户按用户去重，游客按 IP 去重，可取消。

### 用户与权限

- 角色：`admin` / `editor` / `writer` / `visitor`。一个用户可拥有多个角色，以逗号分隔存储。
- 注册、登录、登出、个人资料、修改密码。
- 授权策略：`AdminOnly`、`CanManageContent`（admin + editor）、`CanWrite`（admin + editor + writer）。
- 密码哈希复用框架自带的 `PasswordHasher`（PBKDF2），**不依赖** EF 版 ASP.NET Core Identity。

| 功能 | admin | editor | writer | visitor |
| :--- | :---: | :---: | :---: | :---: |
| 后台仪表盘 | ✅ | ✅ | ➖ | ❌ |
| 文章增删改（全部） | ✅ | ✅ | 仅自己的 | ❌ |
| 独立页面 / 分类 / 标签 | ✅ | ✅ | ❌ | ❌ |
| 评论审核 | ✅ | ✅ | ❌ | ❌ |
| 文件管理（上传 / 目录 / 分享） | ✅ | ✅ | ✅ | ❌ |
| 用户与角色管理 | ✅ | ❌ | ❌ | ❌ |
| 站点设置 / 导航菜单 / 友链 | ✅ | ❌ | ❌ | ❌ |

### 文件管理与分享（后台 `/Admin/Files`）

- **文件夹无限层级**：`ParentId` 递归，左侧目录树 + 面包屑导航；支持新建、重命名、排序、移动、递归删除。移动到自身或子孙目录会被拒绝，避免成环。
- **文件**：按目录浏览、多文件上传到当前目录、批量移动 / 删除、全库文件名搜索、URL 一键复制。
- **分享**：可对**单个文件**或**整个文件夹**生成公开链接；支持有效期（1 / 7 / 30 天 / 永久）与可选访问密码；可随时停用 / 删除，并统计访问次数。
- **分片上传**：单文件最大 2 GB，每片 8 MB，保留直传兜底路径。
- **私有存储**：文件存放在 `wwwroot` 之外的 `App_Data/media`；任何 `/uploads` 请求一律返回 404，内容只能经由登录态或分享令牌授权的管道读取。

### 后台（`/Admin`）

仪表盘统计、写文章、独立页面、分类管理、标签管理、评论审核、用户与角色管理、站点设置、文件管理、**多级导航菜单**、友情链接。

站点设置覆盖：站点名称 / 描述 / 关键词、Logo、每页文章数、评论开关（启用 / 需审核 / 允许游客评论）、注册开关、主题、页脚文本、自定义 `<head>` HTML、首页头部展示方式（默认文案 / 轮播图）与文章列表样式（列表 / 网格）。

导航菜单支持**无限层级**，前台通过递归局部视图渲染为 Bootstrap 下拉菜单（二级起使用 `dropend` 继续展开）。

---

## 技术选型

| 层面 | 选型 | 为什么 |
| --- | --- | --- |
| 框架 | ASP.NET Core MVC（`net10.0`） | 控制器 + Razor 视图，服务端渲染，无 SPA 工具链 |
| ORM | **SqlSugarCore 5.1.4** | CodeFirst 自动建表，不需要迁移工具 |
| 数据库 | SQLite（默认）→ SQL Server / MySQL | 仅改配置即可切换 |
| Markdown | Markdig 1.4.1 | 表格、代码块、任务列表等扩展；原始 HTML 默认关闭 |
| 认证 | Cookie 认证 + 自定义角色声明 | 不引入 EF 版 Identity |
| 前端 | Bootstrap 5 + jQuery（内置于 `wwwroot/lib`） | 无 CDN 依赖，可完全离线运行 |

---

## 环境要求

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) 或更高（`dotnet --version` 应为 `10.0.x`）。
- Windows / Linux / macOS 均可。默认配置下不需要 Node.js、npm 或任何外部数据库。

---

## 快速开始

```bash
git clone https://github.com/Hi-Viken/Quill.git
cd Quill/Quill
dotnet run
```

浏览器打开 <http://localhost:5188>（或控制台输出的端口）。

首次启动会自动完成三件事：

1. 通过 SqlSugar CodeFirst 建表（`DbInitializer.EnsureTables`）；
2. 写入种子数据——示例文章、分类、标签、多级菜单、友情链接（`DbInitializer.Seed`）；
3. 初始化私有媒体目录并修复历史库结构（`FileService.Initialize`）。

SQLite 库文件 `Quill.db` 生成在项目目录下，删除它即得到一份干净安装。

使用 HTTPS 配置启动：

```bash
dotnet run --launch-profile https
# https://localhost:7133 与 http://localhost:5188
```

---

## 默认账号

| 用户名 | 密码 | 角色 | 说明 |
| --- | --- | --- | --- |
| `admin` | `admin123` | admin | 站长，全部权限 |
| `editor` | `editor123` | editor | 编辑 |
| `writer` | `writer123` | writer | 作者，仅能管理自己的文章 |

> ⚠️ **首次登录后请立即修改这些密码**（个人资料 → 修改密码），或在对外开放前于「后台 → 用户管理」中删除种子账号。

新注册用户默认为 `visitor`，需由管理员在「后台 → 用户管理」提升角色后才能写作。

---

## 路由一览

| 路径 | 说明 |
| --- | --- |
| `/` | 首页（置顶优先 + 分页） |
| `/Article/Details/{slug}` | 文章详情 |
| `/Article/Search?q=` | 搜索结果 |
| `/page/{slug}` | 独立页面，如 `/page/about` |
| `/category/{slug}` | 分类归档 |
| `/tag/{slug}` | 标签归档 |
| `/Archive` | 归档总览（按年 / 月） |
| `/Author` | 作者列表 |
| `/Account/Login`、`/Account/Register` | 登录 / 注册 |
| `/Admin` | 后台（需 admin / editor / writer） |
| `/Admin/Files` | 文件管理（目录 + 上传 + 移动 + 分享） |
| `/share/{token}` | 分享访问页（免登录） |
| `/share/{token}/download` | 下载分享的文件或文件夹 |
| `/share/{token}/download/{id}` | 下载分享文件夹中的单个文件 |

路由顺序很关键：`share/*`、`page/{slug}`、`category/{slug}`、`tag/{slug}` 都注册在默认的 `{controller}/{action}` 之前，否则会被约定路由抢先匹配。

---

## 配置说明

`Quill/appsettings.json`：

```json
{
  "Database": {
    "DbType": "Sqlite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Quill.db"
  }
}
```

| 配置项 | 取值 | 说明 |
| --- | --- | --- |
| `Database:DbType` | `Sqlite`（默认）、`SqlServer`、`MySql` | SqlSugar 数据库类型 |
| `Database:LogSql` | `true` / `false` | 控制台输出执行 SQL（仅 Development 生效） |
| `ConnectionStrings:DefaultConnection` | 连接字符串 | SQLite 相对路径会基于内容根目录解析，工作目录变化不会让库文件漂移 |

其余设置（站名、分页条数、评论策略、注册开关、首页布局等）都在「后台 → 站点设置」中运行时修改，保存在 `site_settings` 表。

---

## 切换数据库

默认 SQLite，无需改动。切到 **SQL Server** 只需两步：

```json
{
  "Database": { "DbType": "SqlServer" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Quill;Uid=sa;Pwd=YourPwd;TrustServerCertificate=True"
  }
}
```

MySQL 同理：`DbType` 改为 `MySql`，连接串换成 MySQL 格式。无需改动任何实体或服务代码，启动时会按新库自动建表。

> **关于长文本列**：实体中的长文本（Markdown 正文、摘要、评论内容等）统一使用 `Models.ColumnTypes.LongText`，默认值 `"text"`，在 SQLite、SQL Server、MySQL 中均可正常建表读写。
> 若正式环境用 SQL Server 并希望使用推荐类型，把该常量改为 `"nvarchar(max)"` 即可（注意 SQLite 不支持此写法）。

---

## 目录结构

```
Quill/
├── LICENSE
├── README.md                  # 英文（默认）
├── README.zh-CN.md            # 简体中文
├── Quill.slnx
└── Quill/
    ├── Program.cs             # DI、认证、路由、启动初始化
    ├── Quill.csproj           # net10.0、Markdig、SqlSugarCore
    ├── appsettings.json
    ├── Controllers/           # Home / Article / Page / Category / Tag / Archive /
    │                          # Author / Account / Comment / Like / Media / Share
    ├── Areas/Admin/           # 后台区域：Controllers + ViewModels + Views
    ├── Models/                # 实体、枚举、视图模型
    ├── Services/              # ArticleService、TaxonomyService、CommentService、
    │                          # LikeService、UserService、SettingsService、
    │                          # MarkdownService、FileService、
    │                          # FileManagerService、MediaMarkdownService、
    │                          # MenuLinkResolver
    ├── Data/DbInitializer.cs   # CodeFirst 建表 + 种子数据 + 旧库升级
    ├── Utils/                 # PagedResult、SlugHelper
    ├── Views/                 # 前台视图（Shared 含布局、递归菜单、评论树、文章卡片、分页）
    ├── wwwroot/               # css、js、内置 Bootstrap / jQuery
    └── App_Data/              # media/ 与 temp/，运行时数据，已加入 .gitignore
```

`Quill.db`、`App_Data/`、`bin/`、`obj/`、`.vs/` 均通过 `.gitignore` 排除，媒体文件不会被提交到仓库。

---

## 实现要点

- **评论树**：`CommentService.BuildTree` 用一次 `ToLookup` 装配无限层级，避免逐层递归查询。
- **列表性能**：`ArticleService.LoadExtras` 批量装配作者 / 分类 / 标签 / 评论数 / 点赞数，首页与归档页不会出现 N+1 查询。
- **Markdown 安全**：Markdig 管线调用了 `DisableHtml()`，禁用正文中的原始 HTML 以防 XSS。若需嵌入 HTML（如视频），移除 `MarkdownService` 中该调用即可。
- **评论渲染**：评论内容在输出前做 HTML 编码，即使关闭审核也无法通过评论注入脚本。
- **草稿可见性**：草稿仅管理员 / 编辑、或作者本人可见，其他人访问返回 404 而非 403（不泄露文章是否存在）。
- **私有媒体**：上传文件存放在 `wwwroot` 之外的 `App_Data/media`，每个文件带访问 Key（`FileService`）；`/uploads` 被硬性拦截。仍留在 `wwwroot/uploads` 的历史文件会在启动时迁移。
- **旧库平滑升级**：建表后 `DbInitializer.EnsureTables` 会调用 `EnsureMediaFolderColumn`，为早期没有 `FolderId` 的 `media` 表自动补列，旧库无需手工迁移。
- **多角色存储**：`ApplicationUser.Roles` 以逗号分隔字符串存储，登录时展开为声明，交由标准角色策略校验。
- **Slug**：`SlugHelper` 为文章、页面、分类、标签生成 URL 友好标识；友好路由注册在默认路由之前。

---

## 安全清单

正式上线前请逐项确认：

1. **修改或删除种子账号**（`admin` / `editor` / `writer`）。
2. 将 `Database:LogSql` 设为 `false`（默认也仅在 Development 生效）。
3. 保持媒体目录在 `wwwroot` 之外，不要重新开启 `/uploads` 的静态访问。
4. 置于反向代理（Nginx / Caddy / IIS）之后并启用 TLS，正确转发 `X-Forwarded-Proto` 与 `X-Forwarded-For`，否则 `UseHttpsRedirection` 与按 IP 去重的点赞会失真。
5. 数据库文件与 `App_Data/media` **要一起备份**——库中保存的是相对该目录的媒体路径。
6. 审慎使用「站点设置 → 自定义 head HTML」，该字段有意允许原始标记，且仅管理员可编辑。

---

## 部署

```bash
# 依赖框架的发布
dotnet publish Quill/Quill.csproj -c Release -o ./publish

# 或自包含发布（linux-x64 示例）
dotnet publish Quill/Quill.csproj -c Release -r linux-x64 --self-contained true -o ./publish
```

运行 `publish/Quill.dll`（或 `Quill` 可执行文件），也可托管为服务：

```bash
dotnet publish Quill/Quill.csproj -c Release -o /var/www/quill
# systemd 中 ExecStart 指向：/usr/bin/dotnet /var/www/quill/Quill.dll
```

程序会向内容根目录写入 `Quill.db` 与 `App_Data/`，服务账号需要该目录的写权限。反向代理场景下用 `ASPNETCORE_URLS` 或 `--urls` 绑定本地地址，例如 `http://127.0.0.1:5188`。

---

## 已知限制与规划

已实现：文章、独立页面、分类、标签、归档、作者、搜索、多级评论、点赞、角色权限、文件管理与文件夹分享、多级导航、友情链接、站点设置。

尚未包含，适合作为贡献方向：

- RSS / Atom 订阅与 `sitemap.xml` 生成。
- 定时发布（预约 `PublishedAt`）。
- 媒体缩略图与图片裁剪。
- 可插拔主题（`Theme` 设置已存在，但目前只内置一套主题）。
- 评论通知与密码重置邮件。
- 自动化测试——目前仓库中没有测试项目。

> **NuGet 提示**：构建时可能对 Markdig 的传递依赖 `System.Security.Cryptography.Pkcs 8.0.1` 报 NU1902/NU1903 漏洞告警，不影响构建与运行；如需消除可显式升级该包。

---

## 参与贡献

1. Fork 仓库并新建分支：`git checkout -b feature/my-change`。
2. 保持改动聚焦，遵循现有代码风格（中文 XML 注释与界面文案是有意为之）。
3. 确认 `dotnet build` 通过，并实际点一遍受影响的页面（前台与 `/Admin`）。
4. 提交 Pull Request，说明问题、思路与验证方式。

欢迎提交 Issue，请附上 .NET 版本、数据库类型与相关日志。

---

## 许可协议

基于 [MIT License](LICENSE) 开源。

Copyright (c) 2026 Viken (Hi-Viken)。

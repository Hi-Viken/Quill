# Quill

**A self-hosted blog engine and lightweight CMS built on ASP.NET Core MVC (.NET 10) and SqlSugar — Markdown-native, zero-migration, and single-binary friendly.**

English · [简体中文](README.zh-CN.md)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/)
[![ORM](https://img.shields.io/badge/ORM-SqlSugar-2F6FEB)](https://www.donet5.com/)
[![Storage](https://img.shields.io/badge/Database-SQLite%20%7C%20SQL%20Server%20%7C%20MySQL-003B57?logo=sqlite&logoColor=white)](#switching-databases)
[![License](https://img.shields.io/badge/License-MIT-success.svg)](LICENSE)

---

## Table of Contents

- [Why Quill](#why-quill)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Default Accounts](#default-accounts)
- [Routes](#routes)
- [Configuration](#configuration)
- [Switching Databases](#switching-databases)
- [Project Structure](#project-structure)
- [Implementation Notes](#implementation-notes)
- [Security Checklist](#security-checklist)
- [Deployment](#deployment)
- [Known Limitations & Roadmap](#known-limitations--roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## Why Quill

Most .NET blog engines drag in EF Core migrations, an Identity schema you never asked for, and a themes marketplace you will never use. Quill takes the opposite route:

- **Zero migration files.** SqlSugar CodeFirst creates and upgrades the schema on startup — copy a folder, run `dotnet run`, and you have a working site.
- **Markdown-first writing.** Posts are stored as Markdown, rendered through Markdig with raw HTML disabled by default (XSS-safe out of the box).
- **No CDN, no build step.** Bootstrap 5 and jQuery are vendored under `wwwroot/lib`; there is no `npm install` in your deployment path.
- **One codebase, three databases.** SQLite for a personal box or a side project; flip two config values for SQL Server or MySQL in production.
- **Small, readable surface.** ~11 front-end controllers, ~11 admin controllers, a thin service layer. You can read the whole request path in an afternoon.

---

## Features

### Content

| Feature | Notes |
| --- | --- |
| Articles | Markdown body, draft/published states, sticky (pinned) posts, cover image, excerpt, view counter |
| Standalone pages | e.g. "About" — independent of categories, tags, and the archive |
| Categories | Multi-level via `ParentId`, explicit sort order, per-category archive pages |
| Tags | Many-to-many, tag cloud, per-tag archive pages |
| Archive | Year / month grouping |
| Authors | Author index plus per-author post archive |
| Search | Keyword search across published posts (`/Article/Search?q=`) |

### Interaction

- **Comments** — unlimited nesting via `ParentId`, rendered as a recursive tree; guests and signed-in users can both comment; admin approval toggle; batch approve in the dashboard.
- **Likes** — deduplicated per user when signed in, per IP for guests; toggleable.

### Users & Roles

- Roles: `admin`, `editor`, `writer`, `visitor`. Multiple roles per user, stored comma-separated.
- Registration, login, logout, profile editing, password change.
- Authorization policies: `AdminOnly`, `CanManageContent` (admin + editor), `CanWrite` (admin + editor + writer).
- Passwords are hashed with the framework's `PasswordHasher` (PBKDF2) — ASP.NET Core Identity's EF store is **not** required.

| Capability | admin | editor | writer | visitor |
| :--- | :---: | :---: | :---: | :---: |
| Admin dashboard | ✅ | ✅ | ➖ | ❌ |
| Create / edit / delete any post | ✅ | ✅ | own posts only | ❌ |
| Pages, categories, tags | ✅ | ✅ | ❌ | ❌ |
| Comment moderation | ✅ | ✅ | ❌ | ❌ |
| File manager (upload / folders / shares) | ✅ | ✅ | ✅ | ❌ |
| User & role management | ✅ | ❌ | ❌ | ❌ |
| Site settings, navigation, friend links | ✅ | ❌ | ❌ | ❌ |

### File Manager & Sharing (`/Admin/Files`)

- **Unbounded folder depth** — recursive tree navigation with breadcrumbs; create, rename, reorder, move, and recursive delete. Moves are rejected when the target is the folder itself or one of its descendants, so the tree cannot form a cycle.
- **Files** — browse by folder, multi-file upload into the current folder, batch move/delete, library-wide filename search, one-click URL copy.
- **Shares** — generate a public link for a single file **or an entire folder**; optional expiry (1 / 7 / 30 days / never) and optional access password; disable or delete at any time; visit counter per share.
- **Chunked uploads** — up to 2 GB per file, 8 MB per request chunk, with a direct-upload fallback path.
- **Private storage** — files live in `App_Data/media` **outside** `wwwroot`; every request to `/uploads` is refused with 404, and content is only served through the authenticated or token-authorized pipeline.

### Admin Area (`/Admin/Dashboard`)

Dashboard statistics, post editor, standalone pages, categories, tags, comment moderation, users and roles, site settings, file manager, **multi-level navigation menus**, and friend links.

Site settings cover site name/description/keywords, logo, posts per page, comment toggles (enabled / approval required / guest comments), registration toggle, theme, footer text, custom `<head>` HTML, home hero mode (static text or carousel banners) and article list layout (list or grid).

Navigation menus support unlimited nesting and render recursively as Bootstrap dropdowns (second level and deeper use `dropend`).

---

## Tech Stack

| Layer | Choice | Why |
| --- | --- | --- |
| Framework | ASP.NET Core MVC (`net10.0`) | Controllers + Razor views, server-rendered, no SPA toolchain |
| ORM | **SqlSugarCore 5.1.4** | CodeFirst table creation, no migration tooling |
| Database | SQLite (default) → SQL Server / MySQL | Config-only switch |
| Markdown | Markdig 1.4.1 | Tables, fenced code, task lists; raw HTML disabled |
| Auth | Cookie authentication + custom role claims | No EF-based Identity dependency |
| Front end | Bootstrap 5 + jQuery (vendored in `wwwroot/lib`) | No CDN, works fully offline |

---

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later (`dotnet --version` → `10.0.x`).
- Windows, Linux, or macOS. No Node.js, no npm, no external database required for the default setup.

---

## Quick Start

```bash
git clone https://github.com/Hi-Viken/Quill.git
cd Quill/Quill
dotnet run
```

Then open <http://localhost:5188> (or the port printed in the console).

On first start the application:

1. Creates every table via SqlSugar CodeFirst (`DbInitializer.EnsureTables`).
2. Seeds sample content — posts, categories, tags, a multi-level menu, friend links (`DbInitializer.Seed`).
3. Initializes private media storage and repairs legacy schemas (`FileService.Initialize`).

The SQLite database file `Quill.db` is created in the project directory. Delete it to get a clean install.

To use the HTTPS profile:

```bash
dotnet run --launch-profile https
# https://localhost:7133 and http://localhost:5188
```

---

## Default Accounts

| Username | Password | Role | Notes |
| --- | --- | --- | --- |
| `admin` | `admin123` | admin | Site owner, full access |
| `editor` | `editor123` | editor | Content editor |
| `writer` | `writer123` | writer | Author, manages only their own posts |

> ⚠️ **Change these passwords immediately** after the first login (Profile → Change password), or delete the seeded accounts in **Admin → Users** before exposing the site to the internet.

Newly registered users get the `visitor` role. An administrator must promote them in **Admin → Users** before they can write.

---

## Routes

| Path | Description |
| --- | --- |
| `/` | Home page (pinned posts first, paginated) |
| `/Article/Details/{slug}` | Post detail |
| `/Article/Search?q=` | Search results |
| `/page/{slug}` | Standalone page, e.g. `/page/about` |
| `/category/{slug}` | Category archive |
| `/tag/{slug}` | Tag archive |
| `/Archive` | Archive overview (year / month) |
| `/Author` | Author index |
| `/Account/Login`, `/Account/Register` | Sign in / sign up |
| `/Admin/Dashboard` | Admin dashboard (admin / editor / writer) |
| `/Admin/Files` | File manager (folders, upload, move, share) |
| `/share/{token}` | Public share page (no login) |
| `/share/{token}/download` | Download a shared file or folder bundle |
| `/share/{token}/download/{id}` | Download one file inside a shared folder |

Route ordering matters: `share/*`, `page/{slug}`, `category/{slug}` and `tag/{slug}` are registered **before** the default `{controller}/{action}` route; otherwise the conventional route would swallow them.

---

## Configuration

`Quill/appsettings.json`:

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

| Key | Values | Description |
| --- | --- | --- |
| `Database:DbType` | `Sqlite` (default), `SqlServer`, `MySql` | SqlSugar provider |
| `Database:LogSql` | `true` / `false` | Log executed SQL to the console (Development only) |
| `ConnectionStrings:DefaultConnection` | connection string | Relative SQLite paths resolve against the content root, so the working directory cannot shift the database file |

Everything else — site name, paging size, comment policy, registration, home layout — is edited at runtime in **Admin → Settings** and stored in the `site_settings` table.

---

## Switching Databases

The default SQLite setup needs no changes. For **SQL Server**, two edits in `appsettings.json`:

```json
{
  "Database": { "DbType": "SqlServer" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=Quill;Uid=sa;Pwd=YourPwd;TrustServerCertificate=True"
  }
}
```

MySQL works the same way with `DbType: "MySql"` and a MySQL connection string.

No entity or service code changes: tables are created in the target database on startup.

> **Long-text columns.** Entities declare long text (Markdown body, excerpts, comment content) through `Models.ColumnTypes.LongText`, which defaults to `"text"` — valid in SQLite and accepted by SQL Server and MySQL. If you deploy on SQL Server and prefer the conventional type, change the constant to `"nvarchar(max)"` (note that SQLite does **not** accept that type).

---

## Project Structure

```
Quill/
├── LICENSE
├── README.md                  # English (default)
├── README.zh-CN.md            # 简体中文
├── Quill.slnx
└── Quill/
    ├── Program.cs             # DI, authentication, routing, startup init
    ├── Quill.csproj           # net10.0, Markdig, SqlSugarCore
    ├── appsettings.json
    ├── Controllers/           # Home, Article, Page, Category, Tag, Archive,
    │                          # Author, Account, Comment, Like, Media, Share
    ├── Areas/Admin/           # Admin area: Controllers, ViewModels, Views
    ├── Models/                # Entities, enums, view models
    ├── Services/              # ArticleService, TaxonomyService, CommentService,
    │                          # LikeService, UserService, SettingsService,
    │                          # MarkdownService, FileService,
    │                          # FileManagerService, MediaMarkdownService,
    │                          # MenuLinkResolver
    ├── Data/DbInitializer.cs   # CodeFirst tables + seed data + legacy upgrades
    ├── Utils/                 # PagedResult, SlugHelper
    ├── Views/                 # Front-end Razor views (Shared: layout, recursive
    │                          # menu, comment tree, post card, pagination)
    ├── wwwroot/               # css, js, vendored Bootstrap/jQuery
    └── App_Data/              # media/ and temp/ — runtime data, git-ignored
```

`Quill.db`, `App_Data/`, `bin/`, `obj/` and `.vs/` are excluded via `.gitignore`. Media files are never committed.

---

## Implementation Notes

- **Comment tree** — `CommentService.BuildTree` assembles unlimited nesting with a single `ToLookup`, avoiding recursive queries per level.
- **List performance** — `ArticleService.LoadExtras` batch-loads authors, categories, tags, comment counts and like counts, preventing N+1 queries on the home and archive pages.
- **Markdown safety** — the Markdig pipeline calls `DisableHtml()`, so raw HTML in post bodies cannot be used for XSS. To allow embedded HTML (video embeds, for example), remove that call in `MarkdownService`.
- **Comment rendering** — comment bodies are HTML-encoded before display, so script injection through comments is not possible even with moderation off.
- **Draft visibility** — drafts are visible only to admins/editors and to their own author; anyone else receives 404, not 403 (no existence leak).
- **Private media** — uploaded files are stored outside `wwwroot` under `App_Data/media` with a per-file access key (`FileService`); `/uploads` is hard-blocked. Legacy files that still sit in `wwwroot/uploads` are migrated on startup.
- **Schema repair** — after creating tables, `DbInitializer.EnsureTables` runs `EnsureMediaFolderColumn`, adding the `FolderId` column to old `media` tables automatically. Existing databases need no manual migration.
- **Multi-role storage** — `ApplicationUser.Roles` is a comma-separated string, expanded into claims at sign-in and checked through standard role policies.
- **Slugs** — `SlugHelper` generates URL-friendly identifiers for posts, pages, categories and tags; friendly routes are registered before the default route.

---

## Security Checklist

Before going live:

1. **Change or remove the seeded accounts** (`admin` / `editor` / `writer`).
2. Set `Database:LogSql` to `false` (it is Development-only by default anyway).
3. Keep media outside `wwwroot`; do not re-enable static serving of `/uploads`.
4. Run behind a reverse proxy (Nginx, Caddy, IIS) with TLS, and forward `X-Forwarded-Proto` / `X-Forwarded-For` so `UseHttpsRedirection` and IP-based like deduplication behave correctly.
5. Back up the database file **and** `App_Data/media` together — the database stores media paths relative to that folder.
6. Review **Admin → Settings → Custom head HTML**; that field intentionally allows raw markup and is admin-only.

---

## Deployment

```bash
# publish a framework-dependent build
dotnet publish Quill/Quill.csproj -c Release -o ./publish

# or a self-contained single-directory build for Linux x64
dotnet publish Quill/Quill.csproj -c Release -r linux-x64 --self-contained true -o ./publish
```

Run `publish/Quill.dll` (or the `Quill` executable), or host it as a service:

```bash
# systemd example
dotnet publish Quill/Quill.csproj -c Release -o /var/www/quill
# then point ExecStart at: /usr/bin/dotnet /var/www/quill/Quill.dll
```

The application writes to its content root (`Quill.db`, `App_Data/`), so the service account needs write permission there. Set `ASPNETCORE_URLS` or use `--urls` to bind a specific address behind a reverse proxy, e.g. `http://127.0.0.1:5188`.

---

## Known Limitations & Roadmap

Implemented today: posts, pages, categories, tags, archive, authors, search, nested comments, likes, roles, file manager with folder shares, navigation menus, friend links, site settings.

Not included yet — sensible places to contribute:

- RSS / Atom feed and `sitemap.xml` generation.
- Scheduled publishing (postpone `PublishedAt`).
- Media library thumbnails and image resizing.
- Pluggable themes (the `Theme` setting is stored but a single theme ships today).
- Email notifications for comments and password resets.
- Automated tests — there is currently no test project.

### Dependency security

`System.Security.Cryptography.Pkcs 8.0.1` used to arrive transitively through `SqlSugarCore → Oracle.ManagedDataAccess.Core 23.8.0`, and carried two published advisories:

| ID | Severity | Issue | Patched in |
| --- | --- | --- | --- |
| [CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) | High (7.5) | `SignedCms.CheckSignature` re-parses attacker-controlled X.509 data, allowing unauthenticated quadratic CPU exhaustion | 8.0.2 / 9.0.21 / 10.0.13 |
| [CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) | Moderate (5.9) | `EnvelopedCms.Decode` performs quadratic work on crafted CMS/PKCS#7 attributes (Windows) | 8.0.2 / 9.0.21 / 10.0.13 |

`Quill.csproj` now pins `System.Security.Cryptography.Pkcs` to **10.0.13**, which overrides the transitive 8.0.1 and restores clean (`dotnet list package --vulnerable --include-transitive` reports no vulnerable packages). The assembly is out-of-band — it is not part of the shared framework — so the pinned version is what actually gets deployed.

This pin can be dropped once `Oracle.ManagedDataAccess.Core` requires 8.0.2 or later on its own. The comment in `Quill.csproj` records the dependency chain and both CVEs for whoever revisits it.

---

## Contributing

1. Fork the repository and create a topic branch: `git checkout -b feature/my-change`.
2. Keep changes focused; match the existing code style (Chinese XML comments and UI text are intentional).
3. Verify `dotnet build` succeeds and click through the affected pages, both front-end and the admin area.
4. Open a pull request describing the problem, the approach, and how you verified it.

Bug reports are welcome — please include the .NET version, the database provider, and the relevant log output.

---

## License

Released under the [MIT License](LICENSE).

Copyright (c) 2026 Viken (Hi-Viken).

using Quill.Data;
using Quill.Models;
using Quill.Services;
using SqlSugar;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 0. 上传体积限制（大文件分片上传 + 直传兜底）
// ============================================================
builder.WebHost.ConfigureKestrel(options =>
{
    // 2 GB，实际分片上传每片仅 8 MB，这里给直传留出余量
    options.Limits.MaxRequestBodySize = FileService.MaxUploadBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = FileService.MaxUploadBytes;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.BufferBody = false;
});

// ============================================================
// 1. SqlSugar 配置
//    默认使用 SQLite（文件数据库，开箱即跑）。
//    切换到 SQL Server / MySQL 只需两步：
//      1) appsettings.json 中 Database:DbType 改为 SqlServer（或 MySql）
//      2) ConnectionStrings:DefaultConnection 改为对应连接串，例如：
//         Server=.;Database=Quill;Uid=sa;Pwd=YourPwd;TrustServerCertificate=True
// ============================================================
var dbTypeString = builder.Configuration["Database:DbType"] ?? "Sqlite";
var dbType = Enum.TryParse<DbType>(dbTypeString, true, out var parsedDbType) ? parsedDbType : DbType.Sqlite;

var rawConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=Quill.db";
var connectionString = rawConn;
if (dbType == DbType.Sqlite)
{
    // SQLite 相对路径统一基于内容根目录，避免工作目录变化导致库文件位置漂移
    var file = rawConn.Replace("Data Source=", string.Empty).Trim();
    if (!Path.IsPathRooted(file))
        file = Path.Combine(builder.Environment.ContentRootPath, file);
    connectionString = $"Data Source={file}";
}

var enableSqlLog = builder.Environment.IsDevelopment() &&
                   (builder.Configuration["Database:LogSql"] ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);

builder.Services.AddScoped<ISqlSugarClient>(_ =>
{
    var client = new SqlSugarClient(new ConnectionConfig
    {
        ConfigId = "main",
        ConnectionString = connectionString,
        DbType = dbType,
        IsAutoCloseConnection = true,   // 每次操作后自动关闭连接
        InitKeyType = InitKeyType.Attribute
    });

    if (enableSqlLog)
    {
        client.Aop.OnLogExecuting = (sql, pars) =>
            Console.WriteLine($"[SQL] {sql}");
        client.Aop.OnError = ex => Console.WriteLine($"[SQL ERROR] {ex.Message}");
    }

    return client;
});

// ============================================================
// 2. 业务服务
// ============================================================
builder.Services.AddScoped<MarkdownService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<MenuLinkResolver>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<TaxonomyService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<LikeService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<FileManagerService>();
builder.Services.AddScoped<MediaMarkdownService>();

// ============================================================
// 3. 认证与授权（Cookie 认证 + 多角色策略，不依赖 EF 版 Identity）
// ============================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "Quill.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    // 管理员
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    // 可管理内容（admin / editor）
    options.AddPolicy("CanManageContent", p => p.RequireRole(Roles.Admin, Roles.Editor));
    // 可写作（admin / editor / writer）
    options.AddPolicy("CanWrite", p => p.RequireRole(Roles.Admin, Roles.Editor, Roles.Writer));
});

// ============================================================
// 4. MVC
// ============================================================
builder.Services.AddControllersWithViews();

// 中文区域，保证日期等格式化符合习惯
var zhCulture = new System.Globalization.CultureInfo("zh-CN");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = zhCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = zhCulture;

var app = builder.Build();

// ============================================================
// 5. 中间件管道
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// 媒体文件已迁移到 wwwroot 之外的私有目录，任何 /uploads 静态访问一律拒绝
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.MapStaticAssets();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 后台 Admin 区域路由（{area:exists} 要求 URL 中实际存在区域段，不会劫持前台 /）
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// 分享访问：/share/{token} 与其下载路径
app.MapControllerRoute(
    name: "share-download-file",
    pattern: "share/{token}/download/{id}",
    defaults: new { controller = "Share", action = "Download" });

app.MapControllerRoute(
    name: "share-download",
    pattern: "share/{token}/download",
    defaults: new { controller = "Share", action = "Download" });

app.MapControllerRoute(
    name: "share",
    pattern: "share/{token}",
    defaults: new { controller = "Share", action = "Details" });

// 友好 URL：/page/{slug}、/category/{slug}、/tag/{slug}
// 注意：需置于 default 路由之前，否则会被 {controller}/{action} 抢先匹配
app.MapControllerRoute(
    name: "page-by-slug",
    pattern: "page/{slug}",
    defaults: new { controller = "Page", action = "Details" });

app.MapControllerRoute(
    name: "category-by-slug",
    pattern: "category/{slug}",
    defaults: new { controller = "Category", action = "Details" });

app.MapControllerRoute(
    name: "tag-by-slug",
    pattern: "tag/{slug}",
    defaults: new { controller = "Tag", action = "Details" });

// 前台默认路由
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ============================================================
// 6. 启动时建表 + 种子数据
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<ISqlSugarClient>();
        DbInitializer.EnsureTables(db);
        DbInitializer.Seed(
            db,
            services.GetRequiredService<UserService>(),
            services.GetRequiredService<MarkdownService>());

        // 私有文件存储：补齐访问 Key、把历史 /uploads 文件搬出 wwwroot
        services.GetRequiredService<FileService>().Initialize(db);

        logger.LogInformation("数据库已就绪（{DbType}）", dbType);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "数据库初始化失败");
        throw;
    }
}

app.Run();

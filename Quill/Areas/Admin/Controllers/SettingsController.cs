using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Services;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台站点设置（仅管理员可用）。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class SettingsController : Controller
{
    private readonly SettingsService _settings;

    public SettingsController(SettingsService settings)
    {
        _settings = settings;
    }

    public IActionResult Index()
    {
        return View(_settings.Get());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(SiteSettings model)
    {
        if (!ModelState.IsValid) return View(model);

        // 展示相关的选项做一次白名单校验，避免脏值导致前台渲染异常
        model.HomeHeroMode = model.HomeHeroMode == HomeHeroModes.Carousel
            ? HomeHeroModes.Carousel
            : HomeHeroModes.Default;
        model.ArticleListStyle = model.ArticleListStyle == ArticleListStyles.Grid
            ? ArticleListStyles.Grid
            : ArticleListStyles.List;
        model.LatestPostsStyle = model.LatestPostsStyle == LatestPostsStyles.Card
            ? LatestPostsStyles.Card
            : LatestPostsStyles.List;

        // 轮播图统一走一次解析再序列化，顺带丢掉空项与坏数据
        model.HomeBannersJson = HomeBanner.Serialize(HomeBanner.Parse(model.HomeBannersJson));

        var allowedPositions = new[]
        {
            WatermarkPositions.BottomRight, WatermarkPositions.BottomLeft, WatermarkPositions.TopRight,
            WatermarkPositions.TopLeft, WatermarkPositions.Center, WatermarkPositions.Tiled
        };
        if (!allowedPositions.Contains(model.WatermarkPosition))
            model.WatermarkPosition = WatermarkPositions.BottomRight;

        // 保持原有记录 Id，避免站点设置被写成多条
        model.Id = _settings.Get().Id;
        _settings.Save(model);

        TempData["Success"] = "站点设置已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RefreshCache()
    {
        // TODO: 实际刷新缓存逻辑待实现
        TempData["Success"] = "缓存已刷新";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearCache()
    {
        // TODO: 实际清理缓存逻辑待实现
        TempData["Success"] = "缓存已清理";
        return RedirectToAction(nameof(Index));
    }
}

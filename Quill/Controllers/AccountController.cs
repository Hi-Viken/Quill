using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Models;
using Quill.Models.ViewModels;
using Quill.Services;

namespace Quill.Controllers;

/// <summary>
/// 账户：注册 / 登录 / 登出 / 个人资料
/// 使用 Cookie 认证 + 角色声明，不依赖 EF 版 Identity
/// </summary>
public class AccountController : Controller
{
    private readonly UserService _users;
    private readonly SettingsService _settings;

    public AccountController(UserService users, SettingsService settings)
    {
        _users = users;
        _settings = settings;
    }

    // ---------------- 登录 ----------------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Title"] = "登录";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["Title"] = "登录";

        if (!ModelState.IsValid) return View(model);

        var user = _users.Validate(model.Account, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "用户名或密码错误，或账号已被禁用");
            return View(model);
        }

        await SignInAsync(user, model.RememberMe);
        TempData["Success"] = $"欢迎回来，{user.DisplayNameOrName}！";
        return RedirectToLocal(returnUrl);
    }

    // ---------------- 注册 ----------------

    [HttpGet]
    public IActionResult Register()
    {
        if (!_settings.Get().AllowRegistration)
        {
            TempData["Error"] = "站点已关闭注册";
            return RedirectToAction("Index", "Home");
        }
        ViewData["Title"] = "注册";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        ViewData["Title"] = "注册";

        if (!_settings.Get().AllowRegistration)
        {
            TempData["Error"] = "站点已关闭注册";
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid) return View(model);

        if (_users.UserNameExists(model.UserName))
            ModelState.AddModelError(nameof(model.UserName), "该用户名已被占用");
        if (!string.IsNullOrWhiteSpace(model.Email) && _users.EmailExists(model.Email))
            ModelState.AddModelError(nameof(model.Email), "该邮箱已被注册");

        if (!ModelState.IsValid) return View(model);

        // 新用户默认 visitor 角色，由管理员在后台提升为 writer/editor/admin
        var user = _users.Register(model.UserName, model.Email ?? string.Empty, model.Password, Roles.Visitor);
        await SignInAsync(user, true);

        TempData["Success"] = "注册成功，已自动登录";
        return RedirectToAction("Index", "Home");
    }

    // ---------------- 登出 ----------------

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "已退出登录";
        return RedirectToAction("Index", "Home");
    }

    // ---------------- 个人资料 ----------------

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var user = _users.GetByUserName(User.Identity!.Name!);
        if (user == null) return Challenge();

        ViewData["Title"] = "个人资料";
        var vm = new ProfileViewModel
        {
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Avatar = user.Avatar,
            Bio = user.Bio
        };
        return View(vm);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Profile(ProfileViewModel vm)
    {
        ViewData["Title"] = "个人资料";
        var user = _users.GetByUserName(User.Identity!.Name!);
        if (user == null) return Challenge();

        if (!ModelState.IsValid) return View(vm);

        // 邮箱唯一性
        if (!string.IsNullOrWhiteSpace(vm.Email) && vm.Email != user.Email && _users.EmailExists(vm.Email))
            ModelState.AddModelError(nameof(vm.Email), "该邮箱已被其他账号使用");

        // 修改密码需校验当前密码
        if (!string.IsNullOrWhiteSpace(vm.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(vm.CurrentPassword) ||
                _users.Validate(user.UserName, vm.CurrentPassword) == null)
            {
                ModelState.AddModelError(nameof(vm.CurrentPassword), "当前密码不正确");
            }
        }

        if (!ModelState.IsValid) return View(vm);

        user.DisplayName = vm.DisplayName;
        user.Email = vm.Email;
        user.Avatar = vm.Avatar;
        user.Bio = vm.Bio;
        _users.Save(user);

        if (!string.IsNullOrWhiteSpace(vm.NewPassword))
            _users.ChangePassword(user, vm.NewPassword);

        TempData["Success"] = "资料已更新";
        return RedirectToAction(nameof(Profile));
    }

    // ---------------- 拒绝访问 ----------------

    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "无访问权限";
        return View();
    }

    // ---------------- 私有辅助 ----------------

    /// <summary>写入认证 Cookie（含多角色声明）</summary>
    private async Task SignInAsync(ApplicationUser user, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("DisplayName", user.DisplayNameOrName)
        };

        // 多角色：每个角色一个 Role 声明，供 [Authorize(Roles=...)] 与策略使用
        foreach (var role in user.RoleList)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = persistent });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}

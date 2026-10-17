using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quill.Areas.Admin.ViewModels;
using Quill.Models;
using Quill.Services;
using SqlSugar;

namespace Quill.Areas.Admin.Controllers;

/// <summary>
/// 后台用户管理（仅管理员可用）。
/// </summary>
[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : Controller
{
    private readonly UserService _users;
    private readonly ISqlSugarClient _db;

    public UsersController(UserService users, ISqlSugarClient db)
    {
        _users = users;
        _db = db;
    }

    public IActionResult Index()
    {
        ViewBag.RoleCounts = _users.GetRoleCounts();
        return View(_users.GetAll());
    }

    public IActionResult Edit(int id)
    {
        var user = _users.GetById(id);
        if (user == null) return NotFound();

        return View(new UserEditViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Avatar = user.Avatar,
            Bio = user.Bio,
            IsActive = user.IsActive,
            Roles = user.RoleList
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(UserEditViewModel vm)
    {
        var user = _users.GetById(vm.Id);
        if (user == null) return NotFound();

        if (!ModelState.IsValid) return View(vm);

        var me = CurrentUser();

        user.DisplayName = vm.DisplayName;
        user.Email = vm.Email;
        user.Avatar = vm.Avatar;
        user.Bio = vm.Bio;
        user.IsActive = vm.IsActive;

        var roles = (vm.Roles ?? new List<string>())
            .Where(r => Roles.All.Contains(r))
            .Distinct()
            .ToList();

        // 不允许把自己降权，避免管理员把自己锁在门外
        if (me != null && me.Id == vm.Id && !roles.Contains(Roles.Admin))
        {
            roles.Add(Roles.Admin);
        }

        user.Roles = roles.Count == 0 ? Roles.Visitor : string.Join(",", roles);
        _users.Save(user);

        if (!string.IsNullOrWhiteSpace(vm.NewPassword))
        {
            _users.ChangePassword(user, vm.NewPassword);
        }

        TempData["Success"] = "用户已保存";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var me = CurrentUser();
        if (me != null && me.Id == id)
        {
            TempData["Error"] = "不能删除当前登录的账号";
            return RedirectToAction(nameof(Index));
        }

        _db.Deleteable<ApplicationUser>().Where(u => u.Id == id).ExecuteCommand();
        TempData["Success"] = "用户已删除";
        return RedirectToAction(nameof(Index));
    }

    private ApplicationUser? CurrentUser()
    {
        var name = User.Identity?.Name;
        return string.IsNullOrEmpty(name) ? null : _users.GetByUserName(name);
    }
}

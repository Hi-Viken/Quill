using Microsoft.AspNetCore.Identity;
using Quill.Models;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 用户服务：注册、登录校验、资料维护。
/// 密码哈希复用框架的 PasswordHasher（PBKDF2），无需引入 EF 版 Identity。
/// </summary>
public class UserService
{
    private readonly ISqlSugarClient _db;
    private readonly PasswordHasher<ApplicationUser> _hasher = new();

    public UserService(ISqlSugarClient db)
    {
        _db = db;
    }

    public ApplicationUser? GetById(int id) => _db.Queryable<ApplicationUser>().First(u => u.Id == id);

    public ApplicationUser? GetByUserName(string userName) =>
        _db.Queryable<ApplicationUser>().First(u => u.UserName == userName);

    public bool UserNameExists(string userName) =>
        _db.Queryable<ApplicationUser>().Any(u => u.UserName == userName);

    public bool EmailExists(string email) =>
        !string.IsNullOrWhiteSpace(email) && _db.Queryable<ApplicationUser>().Any(u => u.Email == email);

    /// <summary>登录校验，成功返回用户，失败返回 null</summary>
    public ApplicationUser? Validate(string account, string password)
    {
        var user = _db.Queryable<ApplicationUser>()
            .First(u => u.UserName == account || (u.Email != null && u.Email == account));
        if (user == null || !user.IsActive) return null;
        if (string.IsNullOrEmpty(user.PasswordHash)) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded
            ? user : null;
    }

    /// <summary>注册新用户（默认角色可配置，通常为 visitor）</summary>
    public ApplicationUser Register(string userName, string email, string password, string defaultRole = Roles.Visitor)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            Roles = defaultRole,
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        user.Id = _db.Insertable(user).ExecuteReturnIdentity();
        user.PasswordHash = _hasher.HashPassword(user, password);
        _db.Updateable(user).ExecuteCommand();
        return user;
    }

    /// <summary>修改密码</summary>
    public void ChangePassword(ApplicationUser user, string newPassword)
    {
        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        _db.Updateable(user).ExecuteCommand();
    }

    public void Save(ApplicationUser user) => _db.Updateable(user).ExecuteCommand();

    public List<ApplicationUser> GetAll() =>
        _db.Queryable<ApplicationUser>().OrderBy(u => u.Id, OrderByType.Desc).ToList();

    /// <summary>各角色用户数（仪表盘用）</summary>
    public Dictionary<string, int> GetRoleCounts()
    {
        var users = GetAll();
        var dict = new Dictionary<string, int>();
        foreach (var r in Roles.All)
            dict[r] = users.Count(u => u.RoleList.Contains(r));
        return dict;
    }
}

using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文件管理：分享链接。
/// MediaId 与 FolderId 二者其一有值 —— 分享单个文件或整个文件夹。
/// </summary>
[SugarTable("media_shares")]
public class MediaShare
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>分享令牌，出现在 URL 中（随机不可猜测）</summary>
    [SugarColumn(Length = 40, IsNullable = false)]
    public string Token { get; set; } = string.Empty;

    /// <summary>分享的文件 Id（与 FolderId 二选一）</summary>
    [SugarColumn(IsNullable = true)]
    public int? MediaId { get; set; }

    /// <summary>分享的文件夹 Id（与 MediaId 二选一）</summary>
    [SugarColumn(IsNullable = true)]
    public int? FolderId { get; set; }

    /// <summary>过期时间，null 表示永久有效</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ExpireAt { get; set; }

    /// <summary>访问密码，为空表示无需密码</summary>
    [SugarColumn(Length = 60, IsNullable = true)]
    public string? Password { get; set; }

    public int VisitCount { get; set; } = 0;

    public bool IsEnabled { get; set; } = true;

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ---- 以下为视图/逻辑辅助字段，不参与建表 ----

    [SugarColumn(IsIgnore = true)]
    public string? TargetName { get; set; }

    [SugarColumn(IsIgnore = true)]
    public bool IsFolder => FolderId.HasValue;

    /// <summary>是否已过期</summary>
    [SugarColumn(IsIgnore = true)]
    public bool IsExpired => ExpireAt.HasValue && ExpireAt.Value < DateTime.Now;

    [SugarColumn(IsIgnore = true)]
    public bool IsValid => IsEnabled && !IsExpired;

    [SugarColumn(IsIgnore = true)]
    public string StatusText => !IsEnabled ? "已停用" : (IsExpired ? "已过期" : "生效中");
}

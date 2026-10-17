using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 友情链接
/// </summary>
[SugarTable("friend_links")]
public class FriendLink
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 60, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [SugarColumn(Length = 400, IsNullable = false)]
    public string Url { get; set; } = string.Empty;

    [SugarColumn(Length = 400, IsNullable = true)]
    public string? Logo { get; set; }

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Description { get; set; }

    public int SortOrder { get; set; } = 0;

    public bool IsVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

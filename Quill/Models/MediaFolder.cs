using SqlSugar;

namespace Quill.Models;

/// <summary>
/// 文件管理：文件夹，支持无限层级（ParentId 递归）。
/// ParentId 为 null 表示根目录。
/// </summary>
[SugarTable("media_folders")]
public class MediaFolder
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 120, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>父级文件夹 Id，null 表示位于根目录</summary>
    [SugarColumn(IsNullable = true)]
    public int? ParentId { get; set; }

    public int SortOrder { get; set; } = 0;

    [SugarColumn(ColumnDataType = ColumnTypes.LongText, IsNullable = true)]
    public string? Description { get; set; }

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ---- 以下为视图/逻辑辅助字段，不参与建表 ----

    [SugarColumn(IsIgnore = true)]
    public List<MediaFolder> Children { get; set; } = new();

    [SugarColumn(IsIgnore = true)]
    public bool HasChildren { get; set; }

    /// <summary>直接子文件数量</summary>
    [SugarColumn(IsIgnore = true)]
    public int FileCount { get; set; }

    /// <summary>后台列表展示用的父级名称</summary>
    [SugarColumn(IsIgnore = true)]
    public string? ParentName { get; set; }
}

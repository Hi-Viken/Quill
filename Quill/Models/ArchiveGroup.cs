namespace Quill.Models;

/// <summary>
/// 归档分组项（按年/月聚合）
/// </summary>
public class ArchiveGroup
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
    public string Label => $"{Year}年{Month:D2}月";
}

namespace Quill.Utils;

/// <summary>
/// 通用分页结果
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
}

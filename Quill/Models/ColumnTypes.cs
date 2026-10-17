namespace Quill.Models;

/// <summary>
/// 跨数据库兼容的列类型常量。
/// 实体中需要"长文本"（Markdown 正文、简介、评论内容等）的列统一使用 ColumnTypes.LongText。
/// </summary>
public static class ColumnTypes
{
    /// <summary>
    /// 长文本列类型。
    /// 默认 "text"：SQLite 原生支持（无长度限制），SQL Server 同样接受该类型（虽已标记为弃用但可正常建表读写），
    /// 因此一套实体可同时适配两种数据库而无需改动。
    /// 若正式环境使用 SQL Server 并希望使用推荐类型，可改为 "nvarchar(max)"；
    /// 注意 SQLite 不支持 "nvarchar(max)" 写法，改后仅可用于 SQL Server。
    /// </summary>
    public const string LongText = "text";
}

using Quill.Models;
using Quill.Utils;

namespace Quill.Areas.Admin.ViewModels;

/// <summary>移动目标下拉项</summary>
public class FolderOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>文件夹树（递归局部视图用）</summary>
public class FolderTreeViewModel
{
    public List<MediaFolder> Folders { get; set; } = new();
    public int? CurrentId { get; set; }
}

/// <summary>后台文件管理主页</summary>
public class FileManagerViewModel
{
    /// <summary>当前文件夹 Id，null 表示根目录</summary>
    public int? FolderId { get; set; }

    public MediaFolder? CurrentFolder { get; set; }

    public List<MediaFolder> Breadcrumb { get; set; } = new();

    public List<MediaFolder> FolderTree { get; set; } = new();

    public List<MediaFolder> SubFolders { get; set; } = new();

    public PagedResult<Media> Files { get; set; } = new();

    public string? Keyword { get; set; }

    /// <summary>排序字段：name / date / type / size</summary>
    public string Sort { get; set; } = "name";

    /// <summary>排序方向：asc / desc</summary>
    public string Dir { get; set; } = "asc";

    /// <summary>当前每页文件数</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>子文件夹的分享 Token（文件夹 Id → Token）</summary>
    public Dictionary<int, string> FolderShareTokens { get; set; } = new();

    /// <summary>当前目录（或搜索结果）文件总占用空间</summary>
    public long TotalSize { get; set; }

    /// <summary>移动目标下拉（0 表示根目录）</summary>
    public List<FolderOption> FolderOptions { get; set; } = new();
}

/// <summary>分享管理页</summary>
public class ShareListViewModel
{
    public List<MediaShare> Shares { get; set; } = new();

    /// <summary>站点根地址，用于拼接完整分享链接</summary>
    public string BaseUrl { get; set; } = string.Empty;
}

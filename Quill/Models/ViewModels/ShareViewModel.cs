using Quill.Models;
using Quill.Services;

namespace Quill.Models.ViewModels;

/// <summary>前台分享访问页</summary>
public class ShareViewModel
{
    public MediaShare Share { get; set; } = new();

    /// <summary>分享单个文件时的文件</summary>
    public Media? File { get; set; }

    /// <summary>分享文件夹时的文件列表（含子目录）</summary>
    public List<Media> Files { get; set; } = new();

    public string? FolderName { get; set; }

    /// <summary>是否需要输入访问密码</summary>
    public bool NeedPassword { get; set; }
}

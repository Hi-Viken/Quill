using System.ComponentModel.DataAnnotations;

namespace Quill.Models.ViewModels;

/// <summary>评论/回复提交表单</summary>
public class CommentInputModel
{
    public int? ArticleId { get; set; }
    public int? PageId { get; set; }

    /// <summary>回复的父评论 Id，一级评论为 null</summary>
    public int? ParentId { get; set; }

    [Required(ErrorMessage = "请填写昵称")]
    [StringLength(60)]
    [Display(Name = "昵称")]
    public string AuthorName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [Display(Name = "邮箱（不会公开）")]
    public string? Email { get; set; }

    [Display(Name = "个人网站")]
    public string? Website { get; set; }

    [Required(ErrorMessage = "请填写评论内容")]
    [StringLength(2000, ErrorMessage = "评论内容过长")]
    [Display(Name = "评论内容")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>文章详情页所需数据</summary>
public class ArticleDetailViewModel
{
    public Article Article { get; set; } = null!;

    /// <summary>已审核评论（树形，含 Replies 嵌套）</summary>
    public List<Comment> Comments { get; set; } = new();

    public List<Article> Related { get; set; } = new();

    public int CommentCount { get; set; }

    public int LikeCount { get; set; }

    /// <summary>当前访客是否已点赞</summary>
    public bool Liked { get; set; }

    public bool CanComment { get; set; }

    public CommentInputModel Input { get; set; } = new();
}

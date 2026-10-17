using Markdig;

namespace Quill.Services;

/// <summary>
/// Markdown 渲染服务（基于 Markdig）。
/// 已通过 DisableHtml() 关闭原始 HTML 以规避 XSS；如需在正文中嵌入 HTML，请移除该方法调用。
/// </summary>
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();
    }

    /// <summary>Markdown -> HTML</summary>
    public string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        return Markdown.ToHtml(markdown, _pipeline);
    }

    /// <summary>生成纯文本摘要（先转 HTML 再去标签，取指定长度）</summary>
    public string ToSummary(string? markdown, int length = 160)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = ToHtml(markdown);
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= length ? text : text[..length] + "…";
    }
}

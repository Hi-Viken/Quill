using Quill.Models;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 点赞服务：登录用户按 UserId 去重，游客按 IP 去重，支持取消点赞。
/// </summary>
public class LikeService
{
    private readonly ISqlSugarClient _db;

    public LikeService(ISqlSugarClient db)
    {
        _db = db;
    }

    public int GetCount(int articleId) =>
        _db.Queryable<Like>().Count(l => l.ArticleId == articleId);

    public bool HasLiked(int articleId, int? userId, string? ip)
    {
        if (userId.HasValue)
            return _db.Queryable<Like>().Any(l => l.ArticleId == articleId && l.UserId == userId.Value);
        return !string.IsNullOrWhiteSpace(ip)
            && _db.Queryable<Like>().Any(l => l.ArticleId == articleId && l.IpAddress == ip);
    }

    /// <summary>切换点赞状态，返回（最新总数，当前是否已点赞）</summary>
    public (int Count, bool Liked) Toggle(int articleId, int? userId, string? ip)
    {
        var existing = userId.HasValue
            ? _db.Queryable<Like>().First(l => l.ArticleId == articleId && l.UserId == userId.Value)
            : _db.Queryable<Like>().First(l => l.ArticleId == articleId && l.IpAddress == ip);

        if (existing != null)
        {
            _db.Deleteable<Like>().Where(l => l.Id == existing.Id).ExecuteCommand();
        }
        else
        {
            _db.Insertable(new Like
            {
                ArticleId = articleId,
                UserId = userId,
                IpAddress = userId.HasValue ? null : ip,
                CreatedAt = DateTime.Now
            }).ExecuteCommand();
        }

        var liked = existing == null;
        return (GetCount(articleId), liked);
    }
}

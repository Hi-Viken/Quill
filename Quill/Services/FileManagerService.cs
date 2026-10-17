using Quill.Models;
using Quill.Utils;
using SqlSugar;

namespace Quill.Services;

/// <summary>
/// 文件管理服务：文件夹（无限层级）、文件归属与移动、分享链接。
/// </summary>
public class FileManagerService
{
    private readonly ISqlSugarClient _db;
    private readonly FileService _files;

    public FileManagerService(ISqlSugarClient db, FileService files)
    {
        _db = db;
        _files = files;
    }

    // ================= 文件夹 =================

    /// <summary>全部文件夹（平铺，按排序与名称）</summary>
    public List<MediaFolder> GetAllFolders()
    {
        var list = _db.Queryable<MediaFolder>()
                      .OrderBy(f => f.SortOrder)
                      .OrderBy(f => f.Id)
                      .ToList();

        // 每个文件夹的直接文件数
        var counts = _db.Queryable<Media>()
                        .Where(m => m.FolderId != null)
                        .GroupBy(m => m.FolderId)
                        .Select(m => new { Fid = m.FolderId, Cnt = SqlFunc.AggregateCount(m.Id) })
                        .ToList();
        var dict = counts.Where(x => x.Fid.HasValue).ToDictionary(x => x.Fid!.Value, x => x.Cnt);
        foreach (var f in list) f.FileCount = dict.GetValueOrDefault(f.Id);

        return list;
    }

    /// <summary>文件夹树（根节点 ParentId 为空）</summary>
    public List<MediaFolder> GetFolderTree()
    {
        var all = GetAllFolders();
        var roots = all.Where(f => f.ParentId == null || f.ParentId == 0).ToList();
        BuildChildren(roots, all);
        return roots;
    }

    private static void BuildChildren(List<MediaFolder> parents, List<MediaFolder> all)
    {
        foreach (var p in parents)
        {
            var children = all.Where(f => f.ParentId == p.Id).ToList();
            p.HasChildren = children.Count > 0;
            if (children.Count > 0)
            {
                BuildChildren(children, all);
                p.Children = children;
            }
        }
    }

    public MediaFolder? GetFolder(int id) =>
        _db.Queryable<MediaFolder>().First(f => f.Id == id);

    /// <summary>面包屑：从根到当前文件夹</summary>
    public List<MediaFolder> GetBreadcrumb(int? folderId)
    {
        var result = new List<MediaFolder>();
        var guard = 0;
        var cur = folderId;
        while (cur.HasValue && cur.Value > 0 && guard++ < 32)
        {
            var f = GetFolder(cur.Value);
            if (f == null) break;
            result.Insert(0, f);
            cur = f.ParentId;
        }
        return result;
    }

    /// <summary>当前文件夹的直接子文件夹（按排序）</summary>
    public List<MediaFolder> GetSubFolders(int? folderId)
    {
        var all = GetAllFolders();
        return all.Where(f => (f.ParentId ?? 0) == (folderId ?? 0))
                  .OrderBy(f => f.SortOrder).ThenBy(f => f.Name)
                  .ToList();
    }

    /// <summary>新建文件夹，返回新 Id</summary>
    public int CreateFolder(string name, int? parentId, string? description, int userId)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("文件夹名称不能为空");

        var exists = _db.Queryable<MediaFolder>()
                        .Any(f => f.Name == name && (f.ParentId ?? 0) == (parentId ?? 0));
        if (exists) throw new InvalidOperationException("同级目录下已存在同名文件夹");

        var folder = new MediaFolder
        {
            Name = name,
            ParentId = parentId.HasValue && parentId.Value > 0 ? parentId : null,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            SortOrder = _db.Queryable<MediaFolder>().Count(f => (f.ParentId ?? 0) == (parentId ?? 0)) + 1,
            CreatedById = userId,
            CreatedAt = DateTime.Now
        };
        folder.Id = _db.Insertable(folder).ExecuteReturnIdentity();
        return folder.Id;
    }

    /// <summary>重命名 / 修改描述与排序</summary>
    public void UpdateFolder(int id, string name, string? description, int sortOrder)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("文件夹名称不能为空");

        var folder = GetFolder(id) ?? throw new InvalidOperationException("文件夹不存在");

        var dup = _db.Queryable<MediaFolder>()
                     .Any(f => f.Name == name && f.Id != id && (f.ParentId ?? 0) == (folder.ParentId ?? 0));
        if (dup) throw new InvalidOperationException("同级目录下已存在同名文件夹");

        folder.Name = name;
        folder.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        folder.SortOrder = sortOrder;
        _db.Updateable(folder).ExecuteCommand();
    }

    /// <summary>
    /// 移动文件夹。禁止移动到自身或其子孙（避免成环）。
    /// </summary>
    public void MoveFolder(int id, int? targetParentId)
    {
        var folder = GetFolder(id) ?? throw new InvalidOperationException("文件夹不存在");
        var target = targetParentId.HasValue && targetParentId.Value > 0 ? targetParentId : null;

        if (target.HasValue)
        {
            if (target.Value == id) throw new InvalidOperationException("不能把文件夹移动到自己下面");
            var descendants = GetDescendantIds(id);
            if (descendants.Contains(target.Value))
                throw new InvalidOperationException("不能把文件夹移动到它的子目录里");

            var targetFolder = GetFolder(target.Value) ?? throw new InvalidOperationException("目标文件夹不存在");
            var dup = _db.Queryable<MediaFolder>()
                         .Any(f => f.Name == folder.Name && f.Id != id && (f.ParentId ?? 0) == target.Value);
            if (dup) throw new InvalidOperationException("目标目录下已存在同名文件夹");
            _ = targetFolder;
        }
        else
        {
            var dup = _db.Queryable<MediaFolder>()
                         .Any(f => f.Name == folder.Name && f.Id != id && (f.ParentId ?? 0) == 0);
            if (dup) throw new InvalidOperationException("根目录下已存在同名文件夹");
        }

        folder.ParentId = target;
        _db.Updateable(folder).ExecuteCommand();
    }

    /// <summary>递归收集子孙文件夹 Id（含自身）</summary>
    public List<int> GetDescendantIds(int id)
    {
        var all = _db.Queryable<MediaFolder>().Select(f => new { f.Id, f.ParentId }).ToList();
        var result = new List<int>();
        var queue = new Queue<int>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            result.Add(cur);
            foreach (var c in all.Where(x => (x.ParentId ?? 0) == cur))
                queue.Enqueue(c.Id);
        }
        return result.Distinct().ToList();
    }

    /// <summary>
    /// 删除文件夹：递归删除子文件夹、其下文件（含物理文件）以及相关分享记录。
    /// </summary>
    public void DeleteFolder(int id)
    {
        var ids = GetDescendantIds(id);

        var files = _db.Queryable<Media>().Where(m => ids.Contains(m.FolderId!.Value)).ToList();
        foreach (var f in files)
        {
            _files.Delete(f.StoragePath);
            _db.Deleteable<Media>().Where(m => m.Id == f.Id).ExecuteCommand();
        }

        _db.Deleteable<MediaShare>().Where(s => ids.Contains(s.FolderId!.Value)).ExecuteCommand();
        _db.Deleteable<MediaFolder>().Where(f => ids.Contains(f.Id)).ExecuteCommand();
    }

    // ================= 文件 =================

    /// <summary>分页查询某文件夹下的文件（folderId 为空表示根目录）</summary>
    public PagedResult<Media> GetFiles(int? folderId, int page = 1, int pageSize = 24, string? keyword = null,
        string sort = "name", string dir = "asc")
    {
        page = page < 1 ? 1 : page;
        var total = 0;

        var q = _db.Queryable<Media>();
        if (folderId.HasValue && folderId.Value > 0)
            q = q.Where(m => m.FolderId == folderId.Value);
        else
            q = q.Where(m => m.FolderId == null || m.FolderId == 0);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(m => m.FileName.Contains(kw));
        }

        var items = ApplySort(q, sort, dir).ToPageList(page, pageSize, ref total);
        LoadUploader(items);

        return new PagedResult<Media>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    /// <summary>全库搜索文件（不限目录）</summary>
    public PagedResult<Media> SearchFiles(string? keyword, int page = 1, int pageSize = 24,
        string sort = "name", string dir = "asc")
    {
        page = page < 1 ? 1 : page;
        var total = 0;
        var q = _db.Queryable<Media>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(m => m.FileName.Contains(kw));
        }
        var items = ApplySort(q, sort, dir).ToPageList(page, pageSize, ref total);
        LoadUploader(items);
        return new PagedResult<Media> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>资源管理器式排序：名称 / 日期 / 类型 / 大小</summary>
    private static ISugarQueryable<Media> ApplySort(ISugarQueryable<Media> q, string? sort, string? dir)
    {
        var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        var asc = desc ? OrderByType.Desc : OrderByType.Asc;

        return (sort ?? "name").ToLowerInvariant() switch
        {
            "date" => q.OrderBy(m => m.UploadedAt, asc).OrderBy(m => m.Id, asc),
            "size" => q.OrderBy(m => m.Size, asc).OrderBy(m => m.Id, OrderByType.Desc),
            "type" => q.OrderBy(m => m.ContentType, asc).OrderBy(m => m.FileName, OrderByType.Asc),
            _ => q.OrderBy(m => m.FileName, asc).OrderBy(m => m.Id, OrderByType.Asc)
        };
    }

    /// <summary>当前目录下文件总占用空间（不受分页影响）</summary>
    public long GetFolderTotalSize(int? folderId, string? keyword = null)
    {
        var q = _db.Queryable<Media>();
        if (folderId.HasValue && folderId.Value > 0)
            q = q.Where(m => m.FolderId == folderId.Value);
        else
            q = q.Where(m => m.FolderId == null || m.FolderId == 0);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            q = q.Where(m => m.FileName.Contains(kw));
        }
        return q.Select(m => m.Size).ToList().Sum();
    }

    /// <summary>给文件列表补上有效的分享 Token</summary>
    public void LoadShareTokens(List<Media> items)
    {
        if (items.Count == 0) return;
        var ids = items.Select(m => m.Id).ToHashSet();

        var shares = _db.Queryable<MediaShare>()
                        .Where(s => s.IsEnabled)
                        .ToList()
                        .Where(s => s.MediaId.HasValue && ids.Contains(s.MediaId.Value) && !s.IsExpired)
                        .OrderByDescending(s => s.Id)
                        .ToList();

        foreach (var m in items)
            m.ShareToken = shares.FirstOrDefault(s => s.MediaId == m.Id)?.Token;
    }

    /// <summary>文件夹的分享 Token（Id → Token）</summary>
    public Dictionary<int, string> GetFolderShareTokens(IEnumerable<int> folderIds)
    {
        var ids = folderIds?.Distinct().ToHashSet() ?? new HashSet<int>();
        if (ids.Count == 0) return new Dictionary<int, string>();

        var shares = _db.Queryable<MediaShare>()
                        .Where(s => s.IsEnabled)
                        .ToList()
                        .Where(s => s.FolderId.HasValue && ids.Contains(s.FolderId.Value) && !s.IsExpired)
                        .OrderByDescending(s => s.Id)
                        .ToList();

        var map = new Dictionary<int, string>();
        foreach (var s in shares)
            if (s.FolderId.HasValue && !map.ContainsKey(s.FolderId.Value))
                map[s.FolderId.Value] = s.Token;
        return map;
    }

    private void LoadUploader(List<Media> items)
    {
        if (items.Count == 0) return;
        var ids = items.Select(m => m.UploadedById).Distinct().ToList();
        var users = _db.Queryable<ApplicationUser>().Where(u => ids.Contains(u.Id))
                       .Select(u => new { u.Id, u.UserName, u.DisplayName }).ToList();
        foreach (var m in items)
            m.UploadedByName = users.FirstOrDefault(u => u.Id == m.UploadedById)?.DisplayName;
    }

    public Media? GetFile(int id) => _db.Queryable<Media>().First(m => m.Id == id);

    /// <summary>按对外访问 Key 查文件</summary>
    public Media? GetFileByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _db.Queryable<Media>().First(m => m.FileKey == key);
    }

    /// <summary>保存上传文件记录</summary>
    public int SaveFile(string fileName, string fileKey, string storagePath, string contentType, long size,
        int? folderId, int userId, bool isPublic = false)
    {
        var media = new Media
        {
            FileName = fileName,
            FileKey = fileKey,
            StoragePath = storagePath,
            ContentType = contentType,
            Size = size,
            FolderId = folderId.HasValue && folderId.Value > 0 ? folderId : null,
            UploadedById = userId,
            UploadedAt = DateTime.Now,
            IsPublic = isPublic
        };
        media.Id = _db.Insertable(media).ExecuteReturnIdentity();
        return media.Id;
    }

    /// <summary>移动文件到目标文件夹（null 表示根目录）</summary>
    public void MoveFile(int id, int? folderId)
    {
        var media = GetFile(id) ?? throw new InvalidOperationException("文件不存在");
        media.FolderId = folderId.HasValue && folderId.Value > 0 ? folderId : null;
        _db.Updateable(media).ExecuteCommand();
    }

    public void RenameFile(int id, string fileName, string? alt)
    {
        var media = GetFile(id) ?? throw new InvalidOperationException("文件不存在");
        if (!string.IsNullOrWhiteSpace(fileName)) media.FileName = fileName.Trim();
        media.Alt = string.IsNullOrWhiteSpace(alt) ? null : alt.Trim();
        _db.Updateable(media).ExecuteCommand();
    }

    /// <summary>删除文件（含物理文件与其分享记录）</summary>
    public void DeleteFile(int id)
    {
        var media = GetFile(id);
        if (media == null) return;
        _files.Delete(media.StoragePath);
        _db.Deleteable<MediaShare>().Where(s => s.MediaId == id).ExecuteCommand();
        _db.Deleteable<Media>().Where(m => m.Id == id).ExecuteCommand();
    }

    /// <summary>
    /// 判断某分享是否覆盖指定文件：
    /// 直接分享该文件，或分享的是该文件所在的某个祖先文件夹。
    /// </summary>
    public bool ShareCoversFile(MediaShare? share, Media file)
    {
        if (share == null || file == null) return false;
        if (share.MediaId.HasValue) return share.MediaId.Value == file.Id;
        if (!share.FolderId.HasValue || !file.FolderId.HasValue) return false;
        return GetDescendantIds(share.FolderId.Value).Contains(file.FolderId.Value);
    }

    /// <summary>批量移动文件</summary>
    public void MoveFiles(IEnumerable<int> ids, int? folderId)
    {
        var list = ids?.Distinct().ToList() ?? new List<int>();
        if (list.Count == 0) return;
        var target = folderId.HasValue && folderId.Value > 0 ? folderId : null;
        _db.Updateable<Media>()
           .SetColumns(m => new Media { FolderId = target })
           .Where(m => list.Contains(m.Id))
           .ExecuteCommand();
    }

    /// <summary>统计：文件夹总数、文件总数、占用空间</summary>
    public (int FolderCount, int FileCount, long TotalSize) GetStats()
    {
        var folderCount = _db.Queryable<MediaFolder>().Count();
        var fileCount = _db.Queryable<Media>().Count();
        var size = _db.Queryable<Media>().Sum(m => m.Size);
        return (folderCount, fileCount, size);
    }

    // ================= 分享 =================

    private static string NewToken() =>
        Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..12];

    /// <summary>创建分享，返回 MediaShare（含 Token）</summary>
    public MediaShare CreateShare(int? mediaId, int? folderId, int expireDays, string? password, int userId)
    {
        if (!mediaId.HasValue && !folderId.HasValue)
            throw new InvalidOperationException("请选择要分享的文件或文件夹");

        var share = new MediaShare
        {
            Token = NewToken(),
            MediaId = mediaId,
            FolderId = folderId,
            ExpireAt = expireDays > 0 ? DateTime.Now.AddDays(expireDays) : null,
            Password = string.IsNullOrWhiteSpace(password) ? null : password.Trim(),
            VisitCount = 0,
            IsEnabled = true,
            CreatedById = userId,
            CreatedAt = DateTime.Now
        };
        share.Id = _db.Insertable(share).ExecuteReturnIdentity();
        return share;
    }

    public MediaShare? GetShareByToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return _db.Queryable<MediaShare>().First(s => s.Token == token);
    }

    /// <summary>分享是否有效（启用且未过期）</summary>
    public bool IsShareValid(MediaShare? share) => share != null && share.IsValid;

    /// <summary>记录一次访问</summary>
    public void IncrementVisit(int shareId)
    {
        _db.Updateable<MediaShare>()
           .SetColumns(s => s.VisitCount == s.VisitCount + 1)
           .Where(s => s.Id == shareId)
           .ExecuteCommand();
    }

    /// <summary>后台分享列表（带目标名称）</summary>
    public List<MediaShare> GetShares()
    {
        var shares = _db.Queryable<MediaShare>().OrderBy(s => s.Id, OrderByType.Desc).ToList();
        var fileIds = shares.Where(s => s.MediaId.HasValue).Select(s => s.MediaId!.Value).Distinct().ToList();
        var folderIds = shares.Where(s => s.FolderId.HasValue).Select(s => s.FolderId!.Value).Distinct().ToList();

        var files = fileIds.Count == 0
            ? new List<Media>()
            : _db.Queryable<Media>().Where(m => fileIds.Contains(m.Id))
                 .Select(m => new Media { Id = m.Id, FileName = m.FileName }).ToList();
        var folders = folderIds.Count == 0
            ? new List<MediaFolder>()
            : _db.Queryable<MediaFolder>().Where(f => folderIds.Contains(f.Id))
                 .Select(f => new MediaFolder { Id = f.Id, Name = f.Name }).ToList();

        foreach (var s in shares)
        {
            s.TargetName = s.MediaId.HasValue
                ? files.FirstOrDefault(f => f.Id == s.MediaId!.Value)?.FileName ?? "（文件已删除）"
                : folders.FirstOrDefault(f => f.Id == s.FolderId!.Value)?.Name ?? "（文件夹已删除）";
        }

        return shares;
    }

    /// <summary>停用 / 启用分享</summary>
    public void ToggleShare(int id, bool enabled)
    {
        _db.Updateable<MediaShare>()
           .SetColumns(s => s.IsEnabled == enabled)
           .Where(s => s.Id == id)
           .ExecuteCommand();
    }

    public void DeleteShare(int id)
    {
        _db.Deleteable<MediaShare>().Where(s => s.Id == id).ExecuteCommand();
    }

    /// <summary>某个文件的分享记录</summary>
    public MediaShare? GetShareByMedia(int mediaId) =>
        _db.Queryable<MediaShare>().First(s => s.MediaId == mediaId);

    /// <summary>
    /// 找到一条覆盖该文件的有效分享：优先文件自身的分享，
    /// 其次由近到远查找祖先文件夹的分享。没有则返回 null。
    /// </summary>
    public MediaShare? GetEffectiveShare(Media file)
    {
        if (file == null) return null;

        var direct = _db.Queryable<MediaShare>()
                        .Where(s => s.MediaId == file.Id && s.IsEnabled)
                        .OrderBy(s => s.Id, OrderByType.Desc)
                        .ToList()
                        .FirstOrDefault(s => s.IsValid);
        if (direct != null) return direct;

        if (!file.FolderId.HasValue) return null;

        var folderIds = GetBreadcrumb(file.FolderId.Value).Select(f => f.Id).ToList();
        if (folderIds.Count == 0) return null;

        var shares = _db.Queryable<MediaShare>()
                        .Where(s => s.FolderId != null && folderIds.Contains(s.FolderId.Value) && s.IsEnabled)
                        .OrderBy(s => s.Id, OrderByType.Desc)
                        .ToList()
                        .Where(s => s.IsValid)
                        .ToList();

        // 越靠近文件的文件夹优先
        for (var i = folderIds.Count - 1; i >= 0; i--)
        {
            var fid = folderIds[i];
            var hit = shares.FirstOrDefault(s => s.FolderId == fid);
            if (hit != null) return hit;
        }
        return null;
    }

    /// <summary>分享文件夹时的文件列表（含所有子文件夹中的文件）</summary>
    public List<Media> GetFilesInFolderRecursive(int folderId)
    {
        var ids = GetDescendantIds(folderId);
        return _db.Queryable<Media>()
                  .Where(m => ids.Contains(m.FolderId!.Value))
                  .OrderBy(m => m.FileName)
                  .ToList();
    }
}

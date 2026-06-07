using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;

namespace ZeMail.Application;

public class UnifiedInboxService : IUnifiedInboxService
{
    private readonly IZeMailDbContext _db;

    public UnifiedInboxService(IZeMailDbContext db)
    {
        _db = db;
    }

    public Task<List<Message>> GetMessagesAsync(
        int skip = 0,
        int take = 50,
        bool unreadOnly = false,
        CancellationToken ct = default)
    {
        // Accounts mit aktiviertem Unified Inbox
        var accountIds = _db.Accounts
            .Where(a => a.UnifiedInboxEnabled)
            .Select(a => a.Id)
            .ToHashSet();

        if (accountIds.Count == 0)
            return Task.FromResult(new List<Message>());

        // Nur INBOX-Ordner der jeweiligen Accounts
        var folderIds = _db.Folders
            .Where(f => accountIds.Contains(f.AccountId) &&
                        f.Name.ToLower() == "inbox")
            .Select(f => f.Id)
            .ToHashSet();

        var query = _db.Messages
            .Where(m => folderIds.Contains(m.FolderId) && !m.IsDeleted);

        if (unreadOnly)
            query = query.Where(m => !m.IsRead);

        var result = query
            .OrderByDescending(m => m.ReceivedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var accountIds = _db.Accounts
            .Where(a => a.UnifiedInboxEnabled)
            .Select(a => a.Id)
            .ToHashSet();

        if (accountIds.Count == 0)
            return Task.FromResult(0);

        var folderIds = _db.Folders
            .Where(f => accountIds.Contains(f.AccountId) &&
                        f.Name.ToLower() == "inbox")
            .Select(f => f.Id)
            .ToHashSet();

        var count = _db.Messages
            .Count(m => folderIds.Contains(m.FolderId) &&
                        !m.IsDeleted &&
                        !m.IsRead);

        return Task.FromResult(count);
    }
}
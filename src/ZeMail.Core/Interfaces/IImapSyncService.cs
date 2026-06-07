namespace ZeMail.Core.Interfaces;

public interface IImapSyncService
{
    Task SyncAccountAsync(Guid accountId, CancellationToken ct = default);
    Task SyncFolderAsync(Guid folderId, CancellationToken ct = default);
    Task StartIdleAsync(Guid folderId, CancellationToken ct = default);
}
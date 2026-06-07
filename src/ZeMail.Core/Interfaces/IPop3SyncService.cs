namespace ZeMail.Core.Interfaces;

public interface IPop3SyncService
{
    Task SyncAccountAsync(Guid accountId, CancellationToken ct = default);
}
namespace ZeMail.Core.Interfaces;

public interface IAccountTestService
{
    Task<(bool Success, string Message)> TestImapAsync(
        string host, int port, string username, string password,
        CancellationToken ct = default);
}
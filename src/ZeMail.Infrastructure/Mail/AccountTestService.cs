using MailKit.Net.Imap;
using MailKit.Security;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ZeMail.Core.Interfaces;

namespace ZeMail.Infrastructure.Mail;

public class AccountTestService : IAccountTestService
{
    public async Task<(bool Success, string Message)> TestImapAsync(
        string host, int port, string username, string password,
        CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            client.CheckCertificateRevocation = false;
            client.ServerCertificateValidationCallback =
                (sender, certificate, chain, errors) => true;

            await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(username, password, ct);
            await client.DisconnectAsync(true, ct);

            return (true, "✓ Verbindung erfolgreich! Du kannst jetzt speichern.");
        }
        catch (Exception ex)
        {
            var msg = $"✗ {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException is not null)
                msg += $"\nInner: {ex.InnerException.Message}";
            return (false, msg);
        }
    }
}
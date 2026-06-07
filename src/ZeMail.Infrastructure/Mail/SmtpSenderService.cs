using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using ZeMail.Core.Interfaces;
using ZeMail.Core.Models;
using ZeMail.Infrastructure.Persistence;

namespace ZeMail.Infrastructure.Mail;

public sealed class SmtpSenderService : ISmtpSenderService
{
    private readonly ZeMailDbContext           _db;
    private readonly ILogger<SmtpSenderService> _logger;

    public SmtpSenderService(ZeMailDbContext db, ILogger<SmtpSenderService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task SendAsync(OutgoingMessage outgoing, CancellationToken ct = default)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Id == outgoing.AccountId, ct)
            ?? throw new InvalidOperationException(
                $"Account {outgoing.AccountId} nicht gefunden.");

        var mime = BuildMimeMessage(outgoing, account.EmailAddress, account.Name);

        using var client = new SmtpClient();

        _logger.LogInformation(
            "SMTP verbinde: {Host}:{Port}", account.SmtpHost, account.SmtpPort);

        await client.ConnectAsync(
            account.SmtpHost,
            account.SmtpPort,
            SecureSocketOptions.SslOnConnect,
            ct);

        await client.AuthenticateAsync(account.Username, account.Password, ct);

        await client.SendAsync(mime, ct);
        _logger.LogInformation("Mail gesendet: {Subject}", outgoing.Subject);

        await client.DisconnectAsync(true, ct);
    }

    // ── MimeMessage aufbauen ─────────────────────────────────────────────────
    private static MimeMessage BuildMimeMessage(
        OutgoingMessage outgoing, string fromAddress, string fromName)
    {
        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(fromName, fromAddress));

        foreach (var to  in outgoing.To)
            mime.To.Add(MailboxAddress.Parse(to));

        foreach (var cc  in outgoing.Cc)
            mime.Cc.Add(MailboxAddress.Parse(cc));

        foreach (var bcc in outgoing.Bcc)
            mime.Bcc.Add(MailboxAddress.Parse(bcc));

        mime.Subject = outgoing.Subject;

        var builder = new BodyBuilder
        {
            TextBody = outgoing.BodyText,
            HtmlBody = outgoing.BodyHtml,
        };

        foreach (var file in outgoing.Attachments)
            builder.Attachments.Add(file.FileName, file.Data,
                ContentType.Parse(file.MimeType));

        // iCal-Payload für RSVP-Replies (RFC 5546)
        if (!string.IsNullOrEmpty(outgoing.ICalPayload))
        {
            var calPart = new TextPart("calendar")
            {
                Text = outgoing.ICalPayload
            };
            calPart.ContentType.Parameters.Add("method", "REPLY");
            builder.Attachments.Add(calPart);
        }

        mime.Body = builder.ToMessageBody();

        return mime;
    }
}
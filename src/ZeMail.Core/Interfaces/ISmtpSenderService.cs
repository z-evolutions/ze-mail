using ZeMail.Core.Models;

namespace ZeMail.Core.Interfaces;

public interface ISmtpSenderService
{
    Task SendAsync(OutgoingMessage message, CancellationToken ct = default);
}
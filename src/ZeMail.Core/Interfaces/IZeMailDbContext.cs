using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface IZeMailDbContext
{
    IQueryable<Account>    Accounts    { get; }
    IQueryable<Folder>     Folders     { get; }
    IQueryable<Message>    Messages    { get; }
    IQueryable<Attachment> Attachments { get; }
    IQueryable<Rule>       Rules       { get; }
    IQueryable<Signature>  Signatures  { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
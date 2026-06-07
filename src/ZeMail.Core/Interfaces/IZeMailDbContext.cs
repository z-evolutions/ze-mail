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
    IQueryable<Contact>    Contacts    { get; }
    IQueryable<Tag>        Tags        { get; }
    IQueryable<MessageTag> MessageTags { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
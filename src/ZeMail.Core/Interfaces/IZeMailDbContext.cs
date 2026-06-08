using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface IZeMailDbContext
{
    IQueryable<Account>           Accounts        { get; }
    IQueryable<Folder>            Folders         { get; }
    IQueryable<Message>           Messages        { get; }
    IQueryable<Attachment>        Attachments     { get; }
    IQueryable<Rule>              Rules           { get; }
    IQueryable<Signature>         Signatures      { get; }
    IQueryable<Contact>           Contacts        { get; }
    IQueryable<ContactGroup>      ContactGroups   { get; }
    IQueryable<ContactGroupMember> ContactGroupMembers { get; }
    IQueryable<Tag>               Tags            { get; }
    IQueryable<MessageTag>        MessageTags     { get; }
    IQueryable<TaskItem>          Tasks           { get; }
    IQueryable<CalendarEvent>     CalendarEvents  { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
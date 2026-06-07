using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface ITagService
{
    Task<List<Tag>> GetAllAsync(Guid accountId, CancellationToken ct = default);
    Task<Tag> CreateAsync(Guid accountId, string name, string color, CancellationToken ct = default);
    Task UpdateAsync(Tag tag, CancellationToken ct = default);
    Task DeleteAsync(Guid tagId, CancellationToken ct = default);

    Task AddTagToMessageAsync(Guid messageId, Guid tagId, CancellationToken ct = default);
    Task RemoveTagFromMessageAsync(Guid messageId, Guid tagId, CancellationToken ct = default);
    Task<List<Tag>> GetTagsForMessageAsync(Guid messageId, CancellationToken ct = default);
    Task<List<Message>> GetMessagesByTagAsync(Guid tagId, CancellationToken ct = default);
}
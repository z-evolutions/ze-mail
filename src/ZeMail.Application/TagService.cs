using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;

namespace ZeMail.Application;

public class TagService : ITagService
{
    private readonly IZeMailDbContext _db;
    private readonly ILogger<TagService> _logger;

    public TagService(IZeMailDbContext db, ILogger<TagService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<List<Tag>> GetAllAsync(Guid accountId, CancellationToken ct = default)
        => Task.FromResult(_db.Tags.Where(t => t.AccountId == accountId)
                                   .OrderBy(t => t.Name)
                                   .ToList());

    public async Task<Tag> CreateAsync(Guid accountId, string name, string color, CancellationToken ct = default)
    {
        var exists = _db.Tags.Any(t => t.AccountId == accountId &&
                                       t.Name == name);
        if (exists)
            throw new InvalidOperationException($"Tag '{name}' existiert bereits.");

        var tag = new Tag { AccountId = accountId, Name = name, Color = color };
        _db.Add(tag);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Tag '{Name}' created for account {AccountId}", name, accountId);
        return tag;
    }

    public async Task UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Tag '{Name}' updated", tag.Name);
    }

    public async Task DeleteAsync(Guid tagId, CancellationToken ct = default)
    {
        var tag = _db.Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag is null) return;
        _db.Remove(tag);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Tag {TagId} deleted", tagId);
    }

    public async Task AddTagToMessageAsync(Guid messageId, Guid tagId, CancellationToken ct = default)
    {
        var already = _db.MessageTags.Any(mt => mt.MessageId == messageId && mt.TagId == tagId);
        if (already) return;

        _db.Add(new MessageTag { MessageId = messageId, TagId = tagId });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveTagFromMessageAsync(Guid messageId, Guid tagId, CancellationToken ct = default)
    {
        var mt = _db.MessageTags.FirstOrDefault(mt => mt.MessageId == messageId && mt.TagId == tagId);
        if (mt is null) return;
        _db.Remove(mt);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<Tag>> GetTagsForMessageAsync(Guid messageId, CancellationToken ct = default)
        => Task.FromResult(_db.MessageTags
                              .Where(mt => mt.MessageId == messageId)
                              .Select(mt => mt.Tag)
                              .ToList());

    public Task<List<Message>> GetMessagesByTagAsync(Guid tagId, CancellationToken ct = default)
        => Task.FromResult(_db.MessageTags
                              .Where(mt => mt.TagId == tagId)
                              .Select(mt => mt.Message)
                              .ToList());
}
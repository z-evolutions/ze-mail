using ZeMail.Core.Models;

namespace ZeMail.Core.Interfaces;

public interface ISearchService
{
    Task IndexMessageAsync(Guid messageId, CancellationToken ct = default);
    Task RemoveFromIndexAsync(Guid messageId, CancellationToken ct = default);
    Task<List<SearchResult>> SearchAsync(Guid accountId, string query,
        int limit = 50, CancellationToken ct = default);
}
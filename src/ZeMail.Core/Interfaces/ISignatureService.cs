using ZeMail.Core.Entities;

namespace ZeMail.Core.Interfaces;

public interface ISignatureService
{
    Task<List<Signature>> GetByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<Signature?> GetDefaultAsync(Guid accountId, CancellationToken ct = default);
    Task<Signature> CreateAsync(Signature signature, CancellationToken ct = default);
    Task UpdateAsync(Signature signature, CancellationToken ct = default);
    Task DeleteAsync(Guid signatureId, CancellationToken ct = default);
    Task SetDefaultAsync(Guid signatureId, CancellationToken ct = default);
}
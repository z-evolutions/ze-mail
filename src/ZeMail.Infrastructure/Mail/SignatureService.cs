using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeMail.Infrastructure.Persistence;

namespace ZeMail.Infrastructure.Mail;

public sealed class SignatureService : ISignatureService
{
    private readonly ZeMailDbContext _db;
    private readonly ILogger<SignatureService> _logger;

    public SignatureService(ZeMailDbContext db, ILogger<SignatureService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Signature>> GetByAccountAsync(
        Guid accountId, CancellationToken ct = default)
    {
        return await _db.Signatures
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<Signature?> GetDefaultAsync(
        Guid accountId, CancellationToken ct = default)
    {
        return await _db.Signatures
            .FirstOrDefaultAsync(s => s.AccountId == accountId && s.IsDefault, ct);
    }

    public async Task<Signature> CreateAsync(
        Signature signature, CancellationToken ct = default)
    {
        // Erste Signatur wird automatisch Default
        var hasAny = await _db.Signatures
            .AnyAsync(s => s.AccountId == signature.AccountId, ct);

        if (!hasAny)
            signature.IsDefault = true;

        _db.Signatures.Add(signature);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Signatur erstellt: {Name}", signature.Name);
        return signature;
    }

    public async Task UpdateAsync(Signature signature, CancellationToken ct = default)
    {
        _db.Signatures.Update(signature);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Signatur aktualisiert: {Name}", signature.Name);
    }

    public async Task DeleteAsync(Guid signatureId, CancellationToken ct = default)
    {
        var signature = await _db.Signatures
            .FirstOrDefaultAsync(s => s.Id == signatureId, ct)
            ?? throw new InvalidOperationException(
                $"Signatur {signatureId} nicht gefunden.");

        _db.Signatures.Remove(signature);
        await _db.SaveChangesAsync(ct);

        // War es die Default-Signatur? Dann neue Default setzen
        if (signature.IsDefault)
        {
            var next = await _db.Signatures
                .Where(s => s.AccountId == signature.AccountId)
                .OrderBy(s => s.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (next is not null)
            {
                next.IsDefault = true;
                await _db.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("Signatur gelöscht: {Id}", signatureId);
    }

    public async Task SetDefaultAsync(Guid signatureId, CancellationToken ct = default)
    {
        var signature = await _db.Signatures
            .FirstOrDefaultAsync(s => s.Id == signatureId, ct)
            ?? throw new InvalidOperationException(
                $"Signatur {signatureId} nicht gefunden.");

        // Alle anderen auf IsDefault = false
        var others = await _db.Signatures
            .Where(s => s.AccountId == signature.AccountId && s.IsDefault)
            .ToListAsync(ct);

        foreach (var other in others)
            other.IsDefault = false;

        signature.IsDefault = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Standard-Signatur gesetzt: {Name}", signature.Name);
    }
}
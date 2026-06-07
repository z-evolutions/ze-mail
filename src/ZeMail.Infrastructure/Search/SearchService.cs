using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Interfaces;
using ZeMail.Core.Models;
using ZeMail.Infrastructure.Persistence;

namespace ZeMail.Infrastructure.Search;

public sealed class SearchService : ISearchService
{
    private readonly ZeMailDbContext _db;
    private readonly ILogger<SearchService> _logger;

    public SearchService(ZeMailDbContext db, ILogger<SearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Nachricht indexieren ─────────────────────────────────────────────────
    public async Task IndexMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var msg = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (msg is null) return;

        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        // Erst löschen (upsert via delete+insert)
        await using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM MessageSearch WHERE message_id = @id";
        del.Parameters.Add(new SqliteParameter("@id", msg.Id.ToString()));
        await del.ExecuteNonQueryAsync(ct);

        await using var ins = conn.CreateCommand();
        ins.CommandText = """
            INSERT INTO MessageSearch (message_id, folder_id, subject, from_address, from_name, body_text)
            VALUES (@message_id, @folder_id, @subject, @from_address, @from_name, @body_text)
            """;
        ins.Parameters.Add(new SqliteParameter("@message_id",   msg.Id.ToString()));
        ins.Parameters.Add(new SqliteParameter("@folder_id",    msg.FolderId.ToString()));
        ins.Parameters.Add(new SqliteParameter("@subject",      msg.Subject));
        ins.Parameters.Add(new SqliteParameter("@from_address", msg.FromAddress));
        ins.Parameters.Add(new SqliteParameter("@from_name",    msg.FromName));
        ins.Parameters.Add(new SqliteParameter("@body_text",    msg.BodyText ?? string.Empty));
        await ins.ExecuteNonQueryAsync(ct);

        _logger.LogDebug("Indexiert: {MessageId}", messageId);
    }

    // ── Nachricht aus Index entfernen ────────────────────────────────────────
    public async Task RemoveFromIndexAsync(Guid messageId, CancellationToken ct = default)
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MessageSearch WHERE message_id = @id";
        cmd.Parameters.Add(new SqliteParameter("@id", messageId.ToString()));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Volltext-Suche ───────────────────────────────────────────────────────
    public async Task<List<SearchResult>> SearchAsync(
        Guid accountId, string query, int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        // Ordner-IDs des Accounts holen
        var folderIds = await _db.Folders
            .Where(f => f.AccountId == accountId)
            .Select(f => f.Id.ToString())
            .ToListAsync(ct);

        if (folderIds.Count == 0)
            return [];

        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        var results = new List<SearchResult>();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT message_id, folder_id, subject, from_address, from_name, sent_at_utc, rank
            FROM MessageSearch
            WHERE MessageSearch MATCH @query
              AND folder_id IN (SELECT value FROM json_each(@folder_ids))
            ORDER BY rank
            LIMIT @limit
            """;

        cmd.Parameters.Add(new SqliteParameter("@query",
            SanitizeFtsQuery(query)));
        cmd.Parameters.Add(new SqliteParameter("@folder_ids",
            System.Text.Json.JsonSerializer.Serialize(folderIds)));
        cmd.Parameters.Add(new SqliteParameter("@limit", limit));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SearchResult
            {
                MessageId   = Guid.Parse(reader.GetString(0)),
                FolderId    = Guid.Parse(reader.GetString(1)),
                Subject     = reader.GetString(2),
                FromAddress = reader.GetString(3),
                FromName    = reader.GetString(4),
                SentAtUtc   = reader.IsDBNull(5)
                    ? DateTime.MinValue
                    : DateTime.Parse(reader.GetString(5)),
                Rank        = reader.GetDouble(6),
            });
        }

        return results;
    }

    // ── FTS-Query sanitizen (Sonderzeichen escapen) ──────────────────────────
    private static string SanitizeFtsQuery(string input)
    {
        // Einfache Wörter direkt durchlassen, Rest in Anführungszeichen
        input = input.Trim();
        if (input.Contains('"')) return input; // Nutzer hat selbst Anführungszeichen gesetzt
        return $"\"{input.Replace("\"", "")}\"*"; // Prefix-Suche
    }
}
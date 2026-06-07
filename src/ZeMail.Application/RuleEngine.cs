using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Enums;
using ZeMail.Core.Interfaces;
using ZeMail.Core.Models;

namespace ZeMail.Application;

public class RuleEngine : IRuleEngine
{
    private readonly IZeMailDbContext _db;
    private readonly ILogger<RuleEngine> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RuleEngine(IZeMailDbContext db, ILogger<RuleEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ApplyRulesAsync(Message message, Guid accountId, CancellationToken ct = default)
    {
        var rules = _db.Rules
            .Where(r => r.AccountId == accountId && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToList();

        bool anyFired = false;

        foreach (var rule in rules)
        {
            var conditions = Deserialize<List<RuleCondition>>(rule.ConditionsJson);
            var actions    = Deserialize<List<RuleAction>>(rule.ActionsJson);

            if (conditions is null || actions is null) continue;
            if (!AllConditionsMet(message, conditions)) continue;

            _logger.LogInformation("Rule '{Name}' fired for message {MsgId}", rule.Name, message.Id);
            anyFired = true;

            foreach (var action in actions)
                await ExecuteActionAsync(message, action, ct);

            if (rule.StopProcessing) break;
        }

        if (anyFired)
            await _db.SaveChangesAsync(ct);

        return anyFired;
    }

    // ── Condition evaluation ──────────────────────────────────────────────────

    private static bool AllConditionsMet(Message msg, List<RuleCondition> conditions)
        => conditions.All(c => EvaluateCondition(msg, c));

    private static bool EvaluateCondition(Message msg, RuleCondition c)
    {
        var fieldValue = GetFieldValue(msg, c);

        return c.Operator switch
        {
            RuleConditionOperator.Contains    => fieldValue.Contains(c.Value, StringComparison.OrdinalIgnoreCase),
            RuleConditionOperator.NotContains => !fieldValue.Contains(c.Value, StringComparison.OrdinalIgnoreCase),
            RuleConditionOperator.Equals      => fieldValue.Equals(c.Value, StringComparison.OrdinalIgnoreCase),
            RuleConditionOperator.StartsWith  => fieldValue.StartsWith(c.Value, StringComparison.OrdinalIgnoreCase),
            RuleConditionOperator.EndsWith    => fieldValue.EndsWith(c.Value, StringComparison.OrdinalIgnoreCase),
            RuleConditionOperator.IsTrue      => fieldValue == "true",
            RuleConditionOperator.IsFalse     => fieldValue == "false",
            RuleConditionOperator.GreaterThan => EvalNumeric(fieldValue, c.Value, (a, b) => a > b),
            RuleConditionOperator.LessThan    => EvalNumeric(fieldValue, c.Value, (a, b) => a < b),
            _                                 => false
        };
    }

    private static bool EvalNumeric(string fieldValue, string compareValue, Func<long, long, bool> predicate)
        => long.TryParse(fieldValue, out var fv) && long.TryParse(compareValue, out var cv) && predicate(fv, cv);

    private static string GetFieldValue(Message msg, RuleCondition c) => c.Field switch
    {
        RuleConditionField.From             => msg.FromAddress ?? string.Empty,
        RuleConditionField.To               => msg.ToAddresses ?? string.Empty,
        RuleConditionField.Subject          => msg.Subject ?? string.Empty,
        RuleConditionField.Body             => msg.BodyText ?? string.Empty,
        RuleConditionField.HasAttachment    => "false",
        RuleConditionField.SizeGreaterThan  => "0",
        RuleConditionField.SizeLessThan     => "0",
        RuleConditionField.AnyHeader        => ExtractHeader(msg.HeadersJson, c.HeaderName),
        _                                   => string.Empty
    };

    private static string ExtractHeader(string? headersJson, string? headerName)
    {
        if (string.IsNullOrEmpty(headersJson) || string.IsNullOrEmpty(headerName))
            return string.Empty;
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, _json);
            return dict?.GetValueOrDefault(headerName, string.Empty) ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    // ── Action execution ──────────────────────────────────────────────────────

    private async Task ExecuteActionAsync(Message message, RuleAction action, CancellationToken ct)
    {
        switch (action.ActionType)
        {
            case RuleActionType.MarkAsRead:
                message.IsRead = true;
                break;

            case RuleActionType.MarkAsStarred:
                message.IsStarred = true;
                break;

            case RuleActionType.Delete:
                message.IsDeleted = true;
                break;

            case RuleActionType.MoveToFolder:
                await MoveToFolderAsync(message, action.Parameter, ct);
                break;

            case RuleActionType.Forward:
                _logger.LogWarning("Forward action not yet wired (message {Id})", message.Id);
                break;

            case RuleActionType.AddTag:
                _logger.LogWarning("AddTag action not yet wired (message {Id})", message.Id);
                break;

            case RuleActionType.MarkAsSpam:
                message.IsDeleted = true;
                _logger.LogInformation("Message {Id} marked as spam", message.Id);
                break;
        }
    }

    private async Task MoveToFolderAsync(Message message, string? folderName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return;

        var folder = _db.Folders
            .FirstOrDefault(f => f.AccountId == message.Folder.AccountId
                              && f.Name == folderName);

        if (folder is null)
        {
            _logger.LogWarning("MoveToFolder: folder '{Name}' not found", folderName);
            return;
        }

        message.FolderId = folder.Id;
        await Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T? Deserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, _json); }
        catch { return default; }
    }
}
namespace ZeMail.Core.Models;

public record ContactSuggestion(
    Guid ContactId,
    string DisplayName,
    string EmailAddress
);

public record ImportResult(
    int Imported,
    int Skipped,       // Duplikate
    int Failed,
    List<string> Errors
);
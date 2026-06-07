using ZeMail.Core.Enums;

namespace ZeMail.Core.Models;

public record RuleCondition(
    RuleConditionField Field,
    RuleConditionOperator Operator,
    string Value,            // z.B. "boss@example.com" oder "5000000" (Bytes)
    string? HeaderName = null // nur bei AnyHeader
);

public record RuleAction(
    RuleActionType ActionType,
    string? Parameter = null  // MoveToFolder: Folder-Name; Forward: Adresse; AddTag: Tag-Name
);
namespace ZeMail.Core.Enums;

public enum RuleConditionField
{
    From,
    To,
    Subject,
    Body,
    HasAttachment,
    SizeGreaterThan,   // in Bytes
    SizeLessThan,      // in Bytes
    AnyHeader
}

public enum RuleConditionOperator
{
    Contains,
    NotContains,
    Equals,
    StartsWith,
    EndsWith,
    IsTrue,
    IsFalse,
    GreaterThan,
    LessThan
}

public enum RuleActionType
{
    MoveToFolder,
    MarkAsRead,
    MarkAsStarred,
    Delete,
    Forward,
    AddTag,
    MarkAsSpam
}
namespace ZeMail.Core.Entities;

public class MessageTag
{
    public Guid MessageId { get; set; }
    public Guid TagId { get; set; }

    // Navigation
    public Message Message { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
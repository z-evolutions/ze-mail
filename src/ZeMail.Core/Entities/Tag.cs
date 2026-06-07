namespace ZeMail.Core.Entities;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#00BFFF"; // Hex, Default Cyan

    // Navigation
    public Account Account { get; set; } = null!;
    public ICollection<MessageTag> MessageTags { get; set; } = new List<MessageTag>();
}
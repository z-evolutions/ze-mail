namespace ZeMail.Core.Entities;

public class ContactGroup
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = string.Empty;
    public string? Color      { get; set; } = "#3a3aff";
    public string? Icon       { get; set; } = "👥";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ContactGroupMember> Members { get; set; } = [];
}

public class ContactGroupMember
{
    public Guid         GroupId   { get; set; }
    public Guid         ContactId { get; set; }
    public ContactGroup Group     { get; set; } = null!;
    public Contact      Contact   { get; set; } = null!;
}
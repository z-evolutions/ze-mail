namespace ZeMail.Core.Entities;

public class Contact
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid?    AccountId   { get; set; }
    public string   DisplayName { get; set; } = string.Empty;
    public string   EmailsJson  { get; set; } = "[]";
    public string   PhonesJson  { get; set; } = "[]";
    public string?  Organization { get; set; }
    public string?  Department   { get; set; }
    public string?  Website      { get; set; }
    public string?  Street       { get; set; }
    public string?  City         { get; set; }
    public string?  PostalCode   { get; set; }
    public string?  Country      { get; set; }
    public string?  Notes        { get; set; }
    public string?  VCardRaw     { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Account? Account { get; set; }
}
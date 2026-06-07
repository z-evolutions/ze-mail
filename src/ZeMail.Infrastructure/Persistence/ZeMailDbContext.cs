using Microsoft.EntityFrameworkCore;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;

namespace ZeMail.Infrastructure.Persistence;

public class ZeMailDbContext : DbContext, IZeMailDbContext
{
    // Konkrete DbSet-Properties für Infrastructure-internen Zugriff
    public DbSet<Account>          Accounts          => Set<Account>();
    public DbSet<Folder>           Folders           => Set<Folder>();
    public DbSet<Message>          Messages          => Set<Message>();
    public DbSet<Attachment>       Attachments       => Set<Attachment>();
    public DbSet<PendingOperation> PendingOperations => Set<PendingOperation>();
    public DbSet<Signature>        Signatures        => Set<Signature>();
    public DbSet<Rule>             Rules             => Set<Rule>();

    // IZeMailDbContext — explizite Interface-Implementierung
    IQueryable<Account>    IZeMailDbContext.Accounts    => Set<Account>();
    IQueryable<Folder>     IZeMailDbContext.Folders     => Set<Folder>();
    IQueryable<Message>    IZeMailDbContext.Messages    => Set<Message>();
    IQueryable<Attachment> IZeMailDbContext.Attachments => Set<Attachment>();
    IQueryable<Rule>       IZeMailDbContext.Rules       => Set<Rule>();
    IQueryable<Signature>  IZeMailDbContext.Signatures  => Set<Signature>();

    public ZeMailDbContext(DbContextOptions<ZeMailDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EmailAddress).IsRequired();
            e.HasMany(x => x.Folders)
             .WithOne(x => x.Account)
             .HasForeignKey(x => x.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Signatures)
             .WithOne(x => x.Account)
             .HasForeignKey(x => x.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Folder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AccountId, x.FullPath }).IsUnique();
            e.HasMany(x => x.Messages)
             .WithOne(x => x.Folder)
             .HasForeignKey(x => x.FolderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.FolderId, x.Uid }).IsUnique();
            e.HasMany(x => x.Attachments)
             .WithOne(x => x.Message)
             .HasForeignKey(x => x.MessageId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Attachment>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<PendingOperation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountId);
        });

        modelBuilder.Entity<Signature>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AccountId, x.IsDefault });
        });

        modelBuilder.Entity<Rule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).IsRequired().HasMaxLength(200);
            e.HasOne(r => r.Account)
             .WithMany(a => a.Rules)
             .HasForeignKey(r => r.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.AccountId, r.Priority });
        });
    }
}
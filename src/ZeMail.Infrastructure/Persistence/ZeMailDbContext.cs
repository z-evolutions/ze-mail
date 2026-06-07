using Microsoft.EntityFrameworkCore;
using ZeMail.Core.Entities;
using ZeMail.Core.Interfaces;
using ZeMail.Infrastructure.Persistence.Configurations;

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
    public DbSet<Contact>          Contacts          => Set<Contact>();
    public DbSet<Tag>              Tags              => Set<Tag>();
    public DbSet<MessageTag>       MessageTags       => Set<MessageTag>();
    public DbSet<TaskItem>         Tasks             => Set<TaskItem>();
    public DbSet<CalendarEvent>    CalendarEvents    => Set<CalendarEvent>();

    // IZeMailDbContext — explizite Interface-Implementierung
    IQueryable<Account>       IZeMailDbContext.Accounts       => Set<Account>();
    IQueryable<Folder>        IZeMailDbContext.Folders        => Set<Folder>();
    IQueryable<Message>       IZeMailDbContext.Messages       => Set<Message>();
    IQueryable<Attachment>    IZeMailDbContext.Attachments    => Set<Attachment>();
    IQueryable<Rule>          IZeMailDbContext.Rules          => Set<Rule>();
    IQueryable<Signature>     IZeMailDbContext.Signatures     => Set<Signature>();
    IQueryable<Contact>       IZeMailDbContext.Contacts       => Set<Contact>();
    IQueryable<Tag>           IZeMailDbContext.Tags           => Set<Tag>();
    IQueryable<MessageTag>    IZeMailDbContext.MessageTags    => Set<MessageTag>();
    IQueryable<TaskItem>      IZeMailDbContext.Tasks          => Set<TaskItem>();
    IQueryable<CalendarEvent> IZeMailDbContext.CalendarEvents => Set<CalendarEvent>();

    void IZeMailDbContext.Add<T>(T entity)    => base.Add(entity);
    void IZeMailDbContext.Remove<T>(T entity) => base.Remove(entity);

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

        modelBuilder.Entity<Contact>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(300);
            e.HasIndex(x => x.AccountId);
            e.HasOne(x => x.Account)
             .WithMany()
             .HasForeignKey(x => x.AccountId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.Property(t => t.Color).IsRequired().HasMaxLength(7);
            e.HasIndex(t => new { t.AccountId, t.Name }).IsUnique();
            e.HasOne(t => t.Account)
             .WithMany(a => a.Tags)
             .HasForeignKey(t => t.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageTag>(e =>
        {
            e.HasKey(mt => new { mt.MessageId, mt.TagId });
            e.HasOne(mt => mt.Message)
             .WithMany(m => m.MessageTags)
             .HasForeignKey(mt => mt.MessageId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mt => mt.Tag)
             .WithMany(t => t.MessageTags)
             .HasForeignKey(mt => mt.TagId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(500);
            e.HasOne(t => t.Account)
             .WithMany(a => a.Tasks)
             .HasForeignKey(t => t.AccountId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.LinkedMessage)
             .WithMany(m => m.LinkedTasks)
             .HasForeignKey(t => t.LinkedMessageId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
            e.HasIndex(t => new { t.AccountId, t.IsCompleted, t.DueUtc });
        });

        modelBuilder.ApplyConfiguration(new CalendarEventConfiguration());
    }
}
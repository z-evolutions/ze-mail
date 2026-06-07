using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZeMail.Core.Entities;

namespace ZeMail.Infrastructure.Persistence.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Title).IsRequired().HasMaxLength(500);
        b.Property(e => e.CalDavHref).HasMaxLength(1000);
        b.Property(e => e.CalDavEtag).HasMaxLength(255);
        b.Property(e => e.RecurrenceRule).HasMaxLength(500);

        b.HasIndex(e => new { e.AccountId, e.StartUtc });
        b.HasIndex(e => e.CalDavHref);

        b.HasOne(e => e.Account)
         .WithMany()
         .HasForeignKey(e => e.AccountId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.HasIndex(c => new { c.TelegramAccountId, c.PeerId }).IsUnique();
        
        builder.Property(c => c.TopMessageText).HasMaxLength(1000);
        
        builder.HasOne(c => c.Contact)
            .WithOne(ct => ct.Chat)
            .HasForeignKey<Contact>(ct => ct.ChatId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ContactProfile)
            .WithOne(p => p.Chat)
            .HasForeignKey<ContactProfile>(p => p.ChatId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chat)
            .HasForeignKey(m => m.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
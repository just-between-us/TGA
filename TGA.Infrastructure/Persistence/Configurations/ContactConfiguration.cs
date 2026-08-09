using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasIndex(c => new { c.TelegramAccountId, c.PeerUserId }).IsUnique();
        builder.HasIndex(c => c.ChatId).IsUnique(); 
        builder.Property(c => c.DisplayName).HasMaxLength(256);
    }
}
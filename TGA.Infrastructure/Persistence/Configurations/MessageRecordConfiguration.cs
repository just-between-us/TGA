using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class MessageRecordConfiguration : IEntityTypeConfiguration<MessageRecord>
{
    public void Configure(EntityTypeBuilder<MessageRecord> builder)
    {
        builder.HasIndex(m => new { m.ChatId, m.TelegramMessageId }).IsUnique();
        builder.Property(m => m.Text).HasMaxLength(4000);
        builder.Property(m => m.ContactName).HasMaxLength(256);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class ContactProfileConfiguration : IEntityTypeConfiguration<ContactProfile>
{
    public void Configure(EntityTypeBuilder<ContactProfile> builder)
    {
        builder.HasIndex(p => p.ContactId).IsUnique();
        builder.HasIndex(p => p.ChatId).IsUnique();
        builder.Property(p => p.Notes).HasMaxLength(4000);
        builder.Property(p => p.BehaviorProfile).HasMaxLength(4000);
        builder.Property(p => p.AutoReplyInstructions).HasMaxLength(2000);
    }
}
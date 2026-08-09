using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class TelegramAccountConfiguration : IEntityTypeConfiguration<TelegramAccount>
{
    public void Configure(EntityTypeBuilder<TelegramAccount> builder)
    {
        builder.HasIndex(a => a.TelegramUserId).IsUnique();
    }
}
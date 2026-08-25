using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class LlmProviderSettingsConfiguration : IEntityTypeConfiguration<LlmProviderSettings>
{
    public void Configure(EntityTypeBuilder<LlmProviderSettings> builder)
    {
        builder.HasIndex(s => s.Name).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(128);
        builder.Property(s => s.BaseUrl).HasMaxLength(512);
        builder.Property(s => s.Model).HasMaxLength(256);
        builder.Property(s => s.SystemPrompt).HasMaxLength(8000);
    }
}
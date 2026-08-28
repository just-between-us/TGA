using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class LlmRoleAssignmentConfiguration : IEntityTypeConfiguration<LlmRoleAssignment>
{
    public void Configure(EntityTypeBuilder<LlmRoleAssignment> builder)
    {
        builder.HasIndex(a => a.Role).IsUnique();

        builder.HasOne(a => a.LlmProviderSettings)
            .WithMany()
            .HasForeignKey(a => a.LlmProviderSettingsId)
            .OnDelete(DeleteBehavior.SetNull); 
    }
}
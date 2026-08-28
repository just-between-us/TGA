using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence.Configurations;

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.HasIndex(r => new { r.TelegramAccountId, r.PeerUserId }).IsUnique();
    }
}
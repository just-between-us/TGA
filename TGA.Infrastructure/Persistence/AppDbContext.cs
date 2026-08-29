using Microsoft.EntityFrameworkCore;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<MessageRecord> Messages => Set<MessageRecord>();
    public DbSet<ContactProfile> ContactProfiles => Set<ContactProfile>();
    public DbSet<RuntimeSettings> RuntimeSettings => Set<RuntimeSettings>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<TelegramAccount> Accounts => Set<TelegramAccount>();
    public DbSet<LlmProviderSettings> LlmProviderSettings => Set<LlmProviderSettings>();
    public DbSet<LlmRoleAssignment> LlmRoleAssignments => Set<LlmRoleAssignment>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
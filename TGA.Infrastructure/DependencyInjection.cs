using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGA.Contract.Abstractions;
using TGA.Infrastructure.Agent;
using TGA.Infrastructure.Agent.Tools;
using TGA.Infrastructure.AutoReply;
using TGA.Infrastructure.Diagnostics;
using TGA.Infrastructure.Import;
using TGA.Infrastructure.Llm;
using TGA.Infrastructure.Persistence;
using TGA.Infrastructure.Security;
using TGA.Infrastructure.Telegram;

namespace TGA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config,
        string? contentRootPath = null)
    {
        var connectionString = config.GetConnectionString("Default") ?? "Data Source=telegram_assistant.db";
        if (!string.IsNullOrWhiteSpace(contentRootPath))
        {
            var sqlite = new SqliteConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(sqlite.DataSource) &&
                !sqlite.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase) &&
                !Path.IsPathRooted(sqlite.DataSource))
            {
                sqlite.DataSource = Path.Combine(contentRootPath, sqlite.DataSource);
                connectionString = sqlite.ConnectionString;
            }
        }

        services.AddDbContextFactory<AppDbContext>(opt =>
            opt.UseSqlite(connectionString));

        services.AddDataProtection();

        services.AddHostedService<TelegramSessionRestoreHostedService>();
        
        /* services.AddHostedService<TelegramHealthCheckService>();
         пока отключил, т.к. метод, который там используется не может эффективно дать информацию о текущем статусе авторизации в аккаунт 
         -> а именно если устройство выкинули из другого клиента - то метод все ещё работает, а фактически писать уже нельзя*/
        
        
        services.AddSingleton<IAccountStorageService, AccountStorageService>();
        services.AddSingleton<IMessageStorageService, MessageStorageService>();
        services.AddSingleton<IContactStorageService, ContactStorageService>();
        services.AddSingleton<IContactProfileStorageService, ContactProfileStorageService>();
        services.AddSingleton<IChatStorageService, ChatStorageService>();
        
        services.AddSingleton<TelegramClientFactory>();
        
        services.AddSingleton<TelegramOtherUpdateNotifier>();
        services.AddSingleton<ITelegramOtherUpdateNotifier>(sp => sp.GetRequiredService<TelegramOtherUpdateNotifier>());
        
        services.AddSingleton<ITelegramAuthService, TelegramAuthService>();
        services.AddSingleton<ITelegramMessageService, TelegramMessageService>();
        services.AddSingleton<ITelegramConnectionCheckService, TelegramConnectionCheckService>();
        
        services.AddSingleton<IConnectionStatusService, ConnectionStatusService>();
        services.AddSingleton<ITelegramMessageService, TelegramMessageService>();
        
        services.AddSingleton<ISessionEncryptor, DataProtectionSessionEncryptor>();
        
        services.AddSingleton<TelegramContactSyncService>();
        services.AddSingleton<TelegramPeerDirectory>();
        services.AddSingleton<TelegramPeerResolver>();
        services.AddSingleton<TelegramContactResolver>();
        services.AddSingleton<TelegramDialogSyncService>();
        services.AddSingleton<TelegramLoginPrompt>();
        services.AddSingleton<TelegramSessionRestorer>();
        
        services.AddScoped<IExportImportService, ExportImportService>();
        
        services.AddHttpClient("llm");
        services.AddSingleton<ILlmSettingsStorageService, LlmSettingsStorageService>();
        services.AddSingleton<ILlmClient, OpenAiCompatibleLlmClient>();
        services.AddSingleton<ILlmRoleAssignmentService, LlmRoleAssignmentService>();
        services.AddSingleton<ITriageService, TriageService>();
        services.AddSingleton<AutoReplyDebounceService>();
        services.AddHostedService<AutoReplySubscriberHostedService>();
        
        
        services.AddSingleton<IAgentRunStorageService, AgentRunStorageService>();
        services.AddSingleton<IAgentTool, LocalMessageSearchTool>();
        services.AddSingleton<IAgentTool, RemoteMessageSearchTool>();
        services.AddSingleton<IAgentService, AgentService>();

        return services;
    }
}
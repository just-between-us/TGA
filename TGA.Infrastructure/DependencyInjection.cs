using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TGA.Contract.Abstractions;
using TGA.Infrastructure.Import;
using TGA.Infrastructure.Persistence;
using TGA.Infrastructure.Security;
using TGA.Infrastructure.Telegram;

namespace TGA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContextFactory<AppDbContext>(opt =>
            opt.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=telegram_assistant.db"));

        services.AddDataProtection();

        services.AddHostedService<TelegramSessionRestoreHostedService>();
        services.AddSingleton<IConnectionStatusService, ConnectionStatusService>();
        services.AddSingleton<ISessionEncryptor, DataProtectionSessionEncryptor>();
        services.AddSingleton<IAccountStorageService, AccountStorageService>();
        services.AddSingleton<IMessageStorageService, MessageStorageService>();
        services.AddSingleton<TelegramClientFactory>();
        services.AddSingleton<IChatStorageService, ChatStorageService>();
        services.AddSingleton<IContactProfileStorageService, ContactProfileStorageService>();
        services.AddSingleton<ITelegramMessageService, TelegramMessageService>();
        services.AddSingleton<ITelegramAuthService, TelegramAuthService>();
        services.AddSingleton<IContactStorageService, ContactStorageService>();
        services.AddScoped<IExportImportService, ExportImportService>();

        return services;
    }
}
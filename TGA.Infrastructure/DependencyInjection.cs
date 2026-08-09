using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options; 
using Microsoft.Extensions.DependencyInjection;
using TGA.Contract.Abstractions;
using TGA.Contract.Options;
using TGA.Infrastructure.Persistence;
using TGA.Infrastructure.Security;
using TGA.Infrastructure.Telegram;

namespace TGA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        /*services.Configure<TelegramOptions>(config.GetSection(TelegramOptions.SectionName));
        services.Configure<HistoryLoadOptions>(config.GetSection(HistoryLoadOptions.SectionName));*/

        services.AddDbContextFactory<AppDbContext>(opt =>
            opt.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=telegram_assistant.db"));

        services.AddDataProtection();

        services.AddSingleton<ISessionEncryptor, DataProtectionSessionEncryptor>();
        services.AddSingleton<IAccountStorageService, AccountStorageService>();
        services.AddSingleton<IMessageStorageService, MessageStorageService>();

        services.AddSingleton<TelegramClientFactory>();
        services.AddSingleton<ITelegramMessageService, TelegramMessageService>();
        services.AddSingleton<ITelegramAuthService, TelegramAuthService>();

        return services;
    }
}
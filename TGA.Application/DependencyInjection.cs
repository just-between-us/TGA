using Microsoft.Extensions.DependencyInjection;
using TGA.Application.Chats;

namespace TGA.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ChatQueryService>();
        return services;
    }
}
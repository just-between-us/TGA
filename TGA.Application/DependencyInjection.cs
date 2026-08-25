using Microsoft.Extensions.DependencyInjection;
using TGA.Application.Chats;
using TGA.Application.Llm;

namespace TGA.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ChatQueryService>();
        services.AddScoped<LlmTestService>();
        return services;
    }
}
using Microsoft.Extensions.DependencyInjection;
using TGA.Application.Chats;
using TGA.Application.Llm;
using TGA.Application.Statistics;
using TGA.Contract.Abstractions;

namespace TGA.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ChatQueryService>();
        services.AddScoped<LlmTestService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        return services;
    }
}
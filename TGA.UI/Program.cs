using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using TGA.Application;
using TGA.Contract.Options;
using TGA.Infrastructure;
using TGA.Infrastructure.Diagnostics;
using TGA.Infrastructure.Persistence;
using TGA.UI.Components;

var builder = WebApplication.CreateBuilder(args);
var logSink = new TGA.Infrastructure.Diagnostics.InMemoryLogSink();

builder.Services.AddSingleton<TGA.Contract.Abstractions.IDebugLogSink>(logSink);
builder.Logging.AddProvider(new TGA.Infrastructure.Diagnostics.InMemoryLoggerProvider(logSink));

TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    Console.WriteLine($"⚠️ UNOBSERVED TASK EXCEPTION: {args.Exception}");
    args.SetObserved();
};

builder.Services.AddMudServices();

builder.Services.Configure<AutoReplyOptions>(builder.Configuration.GetSection("AutoReply"));
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<HistoryLoadOptions>(builder.Configuration.GetSection(HistoryLoadOptions.SectionName));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

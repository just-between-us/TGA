using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using TGA.Application;
using TGA.Contract.Abstractions;
using TGA.Contract.Options;
using TGA.Infrastructure.Import;
using TGA.Infrastructure.Persistence;
using TGA.Infrastructure.Security;
using TGA.Infrastructure.Telegram;
using TGA.UI.Components;

var builder = WebApplication.CreateBuilder(args);

TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    Console.WriteLine($"⚠️ UNOBSERVED TASK EXCEPTION: {args.Exception}");
    args.SetObserved();
};

builder.Services.AddMudServices();

builder.Services.AddSingleton<IConnectionStatusService, ConnectionStatusService>();

builder.Services.AddApplication();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.Configure<HistoryLoadOptions>(builder.Configuration.GetSection(HistoryLoadOptions.SectionName));

builder.Services.AddDbContextFactory<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=telegram_assistant.db"));

builder.Services.AddDataProtection();

builder.Services.AddSingleton<ISessionEncryptor, DataProtectionSessionEncryptor>();
builder.Services.AddSingleton<IAccountStorageService, AccountStorageService>();
builder.Services.AddSingleton<IMessageStorageService, MessageStorageService>();

builder.Services.AddSingleton<TelegramClientFactory>();
builder.Services.AddSingleton<IChatStorageService, ChatStorageService>();
builder.Services.AddSingleton<IContactProfileStorageService, ContactProfileStorageService>();
builder.Services.AddSingleton<ITelegramMessageService, TelegramMessageService>();
builder.Services.AddSingleton<ITelegramAuthService, TelegramAuthService>();
builder.Services.AddSingleton<IContactStorageService, ContactStorageService>();
builder.Services.AddScoped<IExportImportService, ExportImportService>();

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

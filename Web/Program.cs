using Web.Components;
using Web.Services;
using Web.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var isPlayer2 = args.Contains("--player2");
var playerInfo = isPlayer2 
    ? new PlayerInfo(2, 6000, 6100) 
    : new PlayerInfo(1, 5000, 5100);

builder.Services.AddSingleton(playerInfo);
builder.Services.AddSingleton<SC2Client>();
builder.Services.AddSingleton<LinuxHeadlessClientService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LinuxHeadlessClientService>());

builder.WebHost.UseUrls($"http://0.0.0.0:{playerInfo.WebsitePort}");

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

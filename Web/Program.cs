using Web.Components;
using Web.Models;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSingleton<ClientManager>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<ClientManager>().Player1.PlayerInfo);
builder.Services.AddSingleton(sp => sp.GetRequiredService<ClientManager>().Player1.SC2Client);
builder.Services.AddSingleton(sp =>
  sp.GetRequiredService<ClientManager>().Player1.LinuxHeadlessClientService
);
builder.Services.AddHostedService(sp =>
  sp.GetRequiredService<ClientManager>().Player1.LinuxHeadlessClientService
);

builder.WebHost.UseUrls($"http://0.0.0.0:5100");

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

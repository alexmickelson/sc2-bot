using Microsoft.AspNetCore.Components.Server.Circuits;
using Web.Components;
using Web.Models;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSingleton<ClientManager>();
builder.Services.AddScoped<TrackingCircuitHandler>(); //properly dispose on page refresh
builder.Services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<TrackingCircuitHandler>());

builder.WebHost.UseUrls($"http://0.0.0.0:5000");

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

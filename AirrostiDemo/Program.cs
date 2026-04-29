using AirrostiDemo;
using AirrostiDemo.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// ---------------------------------------------------------------------------
// AirrostiDemo (Blazor WebAssembly) composition root.
//
// This file boots the WASM runtime, registers root components against DOM
// selectors in wwwroot/index.html, wires up DI, and then hands control off
// to the framework.
// ---------------------------------------------------------------------------

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Mount the top-level <App> component into <div id="app"> in index.html.
// Anything placed inside <App> is the routed Blazor application.
builder.RootComponents.Add<App>("#app");
// Inject head fragments (PageTitle, etc.) just after the document <head>.
builder.RootComponents.Add<HeadOutlet>("head::after");

// ---------- DI registrations -----------------------------------------------
// AuthService is scoped (one per circuit; in WASM that's effectively a
// singleton). AuthHeaderHandler is transient because DelegatingHandlers in
// the IHttpClientFactory pipeline are tracked by lifetime internally.
builder.Services.AddScoped<AuthService>();
builder.Services.AddTransient<AuthHeaderHandler>();

// ---------- API base address ----------------------------------------------
// The standalone WASM client has no inherent server origin, so we read the
// API URL from configuration. Resolved from wwwroot/appsettings.{Env}.json
// — Development => https://localhost:7204, Production => the Render URL.
// Fail loud and early if it's missing rather than ship a build that
// silently 404s every request.
var apiBase = builder.Configuration["ApiBaseAddress"]
    ?? throw new InvalidOperationException(
        "ApiBaseAddress not configured — set it in wwwroot/appsettings.{Environment}.json.");

// ---------- HttpClient pipeline --------------------------------------------
// Register a NAMED client whose pipeline runs every request through
// AuthHeaderHandler — that's how the bearer token gets attached
// transparently. Pages that simply @inject HttpClient need no extra setup.
builder.Services.AddHttpClient("AirrostiApi", c =>
    {
        c.BaseAddress = new Uri(apiBase);
    })
    .AddHttpMessageHandler<AuthHeaderHandler>();

// Make the named client the DEFAULT HttpClient for the application — the
// one that Blazor resolves when components ask for a plain HttpClient.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AirrostiApi"));

await builder.Build().RunAsync();

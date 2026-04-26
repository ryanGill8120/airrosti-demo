using AirrostiDemo;
using AirrostiDemo.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<AuthService>();
builder.Services.AddTransient<AuthHeaderHandler>();

var apiBase = builder.Configuration["ApiBaseAddress"]
    ?? throw new InvalidOperationException(
        "ApiBaseAddress not configured — set it in wwwroot/appsettings.{Environment}.json.");

builder.Services.AddHttpClient("AirrostiApi", c =>
    {
        c.BaseAddress = new Uri(apiBase);
    })
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("AirrostiApi"));

await builder.Build().RunAsync();

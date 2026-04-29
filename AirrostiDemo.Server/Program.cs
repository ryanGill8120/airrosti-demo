using System.Text;
using AirrostiDemo.Server.Data;
using AirrostiDemo.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ---------------------------------------------------------------------------
// AirrostiDemo.Server composition root.
//
// This file is the single place where every cross-cutting concern is wired:
//   * MVC controllers + Swagger
//   * Typed HttpClient for OpenFDA
//   * EF Core / SQLite + Identity
//   * JWT Bearer authentication and authorization
//   * CORS for the standalone Blazor WASM client
//   * Forwarded-headers handling for Render's reverse proxy
//
// Order matters: services first (builder.Services.Add*), then the request
// pipeline (app.Use*). Reordering middleware can silently break auth.
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// ---------- MVC + Swagger ---------------------------------------------------
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Describe how protected endpoints are authenticated so Swagger UI shows
    // an "Authorize" button and adds the Bearer header to "Try it out" calls.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste a JWT token (without the 'Bearer ' prefix).",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

// ---------- OpenFDA HTTP client --------------------------------------------
// Register a NAMED HttpClient pointing at api.fda.gov. Using IHttpClientFactory
// gives us connection pooling, DNS refresh, and easy customization without
// the lifetime headaches of holding our own HttpClient instance.
builder.Services.AddHttpClient(OpenFdaClient.HttpClientName, c =>
{
    c.BaseAddress = new Uri("https://api.fda.gov/");
});
// Our typed wrapper is the only thing that should ever resolve the named
// client above — controllers depend on OpenFdaClient, not the factory.
builder.Services.AddScoped<OpenFdaClient>();

// ---------- EF Core + SQLite -----------------------------------------------
// Connection string lives at ConnectionStrings:DefaultConnection (typically
// "Data Source=demo.db"). The DB file is auto-migrated at startup further
// down so a fresh checkout boots without any manual EF commands.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- ASP.NET Core Identity ------------------------------------------
// AddIdentityCore (NOT AddIdentity) because we don't need the cookie-based
// SignInManager surface — this server only authenticates via JWT.
builder.Services.AddIdentityCore<AppUser>(o =>
    {
        // Relaxed password policy for the demo: minimum 6 chars, no
        // class-of-character requirements. Production should tighten these.
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredLength = 6;
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ---------- JWT Bearer authentication --------------------------------------
// Configure how incoming bearer tokens are validated. The values here MUST
// match the ones JwtTokenService.Create() bakes into the token at issue
// time — otherwise tokens our own server signs will fail validation.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            // Symmetric HMAC key — same bytes used for sign + verify. In
            // production the key value comes from a Render env var
            // (Jwt__Key), generated with `generateValue: true` in render.yaml.
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key not configured"))),
            // Tighten the default 5-minute clock skew so an expired token
            // really does become unusable within 30 seconds.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenService>();

// ---------- CORS ------------------------------------------------------------
// Read the allowed origins from configuration as a comma-separated list. In
// dev this falls back to the WASM client's HTTPS launch URL so the demo
// "just works" out of the box.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "https://localhost:7168")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    // Named policy applied below in the request pipeline. We allow any
    // method/header because the API is small and well-defined; we lock
    // the ORIGIN list down hard to keep that surface controlled.
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ---------- Forwarded headers ----------------------------------------------
// Render terminates TLS at its proxy and forwards plain HTTP, so trust the
// X-Forwarded-* headers it sets. Without this, Request.Scheme is "http" in
// production and any URL generation / scheme-aware logic would be wrong.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clearing the default trusted-network lists lets us accept the
    // forwarded headers from Render's edge — those defaults only trust
    // localhost which would silently drop the headers in production.
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// MUST run before anything that inspects scheme/host (auth, CORS, redirects)
// or those components will see the proxy hop instead of the real request.
app.UseForwardedHeaders();

// ---------- DB auto-migration ----------------------------------------------
// Apply pending EF Core migrations on startup so the demo "just runs".
// Wrapped in a DI scope because DbContext is scoped — resolving it from the
// root provider would crash with a captive-dependency error.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ---------- Request pipeline -----------------------------------------------
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Swagger is dev-only — production builds don't expose /swagger.
    app.UseSwagger();
    app.UseSwaggerUI();
    // Render handles TLS at the proxy; only redirect in dev where Kestrel terminates HTTPS.
    app.UseHttpsRedirection();
}

// CORS must run before auth so the preflight OPTIONS request gets matched
// against our policy rather than rejected by the auth middleware.
app.UseCors("BlazorClient");

// Authentication populates HttpContext.User from the validated JWT;
// authorization then checks [Authorize] attributes against that user.
// Order is mandatory: Authentication BEFORE Authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

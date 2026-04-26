# CLAUDE.md
This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project purpose
A demo for a non-invasive recovery clinic showcasing .NET + Blazor full-stack
skills. The app lets users explore FDA adverse event data for pain medications
(via OpenFDA), then optionally save personalized reports behind authentication.

See `docs/PLAN.md` for the phased build plan and `docs/DECISIONS.md` for
architectural rationale.

## Solution layout
Three .NET 8 projects coordinated by `AirrostiDemo.sln`:
- **AirrostiDemo** — Blazor WebAssembly client (`Microsoft.NET.Sdk.BlazorWebAssembly`). Standalone WASM, not hosted by the server project.
- **AirrostiDemo.Server** — ASP.NET Core Web API (`Microsoft.NET.Sdk.Web`) with Swashbuckle/Swagger.
- **AirrostiDemo.Shared** — Plain class library holding DTOs referenced by both client and server.

The client and server are decoupled: the client talks to the server over HTTPS as a separate origin, so both must be running for data calls to work.

## Cross-project wiring (read this before changing URLs or DTOs)
The dev URLs and CORS policy must stay in sync across config files:
- `AirrostiDemo/wwwroot/appsettings.Development.json` — `ApiBaseAddress = https://localhost:7204` (the server's HTTPS launch URL). Read by client `Program.cs` via `builder.Configuration["ApiBaseAddress"]`.
- `AirrostiDemo/wwwroot/appsettings.Production.json` — production `ApiBaseAddress` (currently `https://airrosti-server.onrender.com`). Standalone Blazor WASM defaults to the Production environment when not on localhost.
- `AirrostiDemo.Server/Program.cs` — CORS policy `"BlazorClient"` reads allowed origins from `Cors:AllowedOrigins` (comma-separated). Falls back to `https://localhost:7168` when unset (dev convenience).
- `AirrostiDemo.Server/appsettings.Production.json` — sets `Cors:AllowedOrigins` for production.
- `AirrostiDemo.Server/Properties/launchSettings.json` and `AirrostiDemo/Properties/launchSettings.json` — define dev URLs (`https` profiles: server 7204, client 7168).

If you change either launch URL, update the corresponding config or the WASM client will silently fail to fetch. For prod URL changes, update both `wwwroot/appsettings.Production.json` and the server's `Cors:AllowedOrigins` (in `appsettings.Production.json` or the Render env var `Cors__AllowedOrigins`).

DTOs flow client → server through `AirrostiDemo.Shared`. New API contracts belong in `AirrostiDemo.Shared/` organized by topic folder (`Models/` for the legacy template DTO, `OpenFda/` for FDA response shapes, future `Auth/` for credentials). Don't duplicate model classes in either side.

## External APIs
- **OpenFDA** (`https://api.fda.gov/drug/event.json`) — public, no auth, no key required for demo-level traffic. The server proxies these calls so the client never hits FDA directly.
- Named `HttpClient` `"openFDA"` (constant `OpenFdaClient.HttpClientName`) is registered in `AirrostiDemo.Server/Program.cs` with `BaseAddress = https://api.fda.gov/`.
- `AirrostiDemo.Server/Services/OpenFdaClient.cs` wraps that client and is the only place that should call FDA. Controllers inject `OpenFdaClient`, not `IHttpClientFactory` directly.
- OpenFDA returns `404` when a drug has no matching events — `OpenFdaClient` translates that into an empty `FdaCountResponse` rather than throwing.
- The proxied endpoint is `GET /api/DrugSideEffects/{drugName}?limit=10` (limit clamped 1–50). Do not call OpenFDA from the WASM client.

## Authentication & storage
- **ASP.NET Core Identity + JWT bearer tokens.** No cookies, no external IdP. Server uses `AddIdentityCore<AppUser>` (no roles, no SignInManager cookie surface).
- Server `Data/` folder hosts `AppUser : IdentityUser`, `AppDbContext : IdentityDbContext<AppUser>`, and the `SavedReport` entity. `JwtTokenService` in `Services/` mints tokens from `Jwt:Key|Issuer|Audience|ExpiresMinutes` config — `sub` claim = `AppUser.Id`, which JwtBearer maps to `ClaimTypes.NameIdentifier` for the controller side.
- Connection string lives at `ConnectionStrings:DefaultConnection` (`Data Source=demo.db`) in `appsettings.Development.json` — same file holds `Jwt:` settings. Production overrides via `appsettings.Production.json` / Render env vars.
- **EF Core + SQLite**. The DB file lives alongside the server binary so it travels with the deployment. Server **auto-migrates at startup** (`db.Database.Migrate()`), so a clean checkout just runs — no manual `dotnet ef database update`. Schema changes still need a new migration: `dotnet ef migrations add <Name> -p AirrostiDemo.Server -s AirrostiDemo.Server`.
- **SQLite gotcha**: it can't `ORDER BY DateTimeOffset`. Server entities use `DateTime` (UTC) for sortable timestamps; controllers attach `DateTimeKind.Utc` when projecting to DTOs.
- Client `Services/` folder hosts `AuthService` and `AuthHeaderHandler`. The default injected `HttpClient` is built via `IHttpClientFactory` (named client `"AirrostiApi"`) and runs every request through `AuthHeaderHandler` — new pages that inject `HttpClient` need no extra setup. Tokens land in `localStorage` (`airrosti_token`, `airrosti_expires`) via `IJSRuntime`. Note: `Microsoft.Extensions.Http` package was added to the WASM client so `AddHttpClient` resolves there.
- Protected endpoints use `[Authorize]`. User-scoped queries pull `UserId` from `ClaimTypes.NameIdentifier` — never trust a user ID sent from the client.

### Auth/Reports endpoints
- `POST /api/Auth/register`, `POST /api/Auth/login` — anonymous. Login returns `{ token, expiresAt }`.
- `POST /api/Reports`, `GET /api/Reports` — `[Authorize]`. POST body is `FdaCountResponse`; GET returns `SavedReportDto[]` ordered `SavedAt desc`, scoped to the calling user.

### Client routes
- `/drugs` (anonymous) — search + "Save my results" button (opens auth modal if not signed in).
- `/my-reports` (auth-gated UI) — lists the caller's saved searches.

## Common commands
Run from the repo root:
```bash
# Build the whole solution
dotnet build AirrostiDemo.sln

# Run server (Swagger UI at https://localhost:7204/swagger)
dotnet run --project AirrostiDemo.Server --launch-profile https

# Run WASM client (https://localhost:7168) — server must also be running
dotnet run --project AirrostiDemo --launch-profile https

# EF Core migrations (run from AirrostiDemo.Server/)
dotnet ef migrations add <Name>
dotnet ef database update

# Restore / clean
dotnet restore AirrostiDemo.sln
dotnet clean AirrostiDemo.sln
```

There is no test project in the solution.

## API conventions
- Controllers live in `AirrostiDemo.Server/Controllers/` and follow `[Route("api/[controller]")]`.
- The client's `HttpClient` calls are case-sensitive against the controller name on some hosts — `Pages/Weather.razor` uses `api/WeatherForecast` (matches `WeatherForecastController`). Match controller casing when adding new fetch calls.
- Swagger is only registered in `Development`; production builds won't expose `/swagger`.
- Auth controller endpoints (`/api/auth/register`, `/api/auth/login`) are anonymous. All other write endpoints default to `[Authorize]`.

## Deployment
Target host: **Render** (free tier with $7/mo Starter for the server to avoid cold starts; static site is free).
- `render.yaml` at the repo root is a Render Blueprint defining both services. Push to `master` → Render auto-redeploys whichever service's tracked files changed.
- Server: Docker web service built from `AirrostiDemo.Server/Dockerfile` (multi-stage, .NET 8 SDK → ASP.NET runtime). Listens on `:10000` (Render's default Docker PORT).
- Client: Render Static Site. Build command installs the .NET 8 SDK into the build container, runs `dotnet publish AirrostiDemo`, and serves `publish/wwwroot`. SPA rewrite rule sends every path to `index.html`.
- `Jwt__Key` is generated by Render (`generateValue: true`) on first Blueprint sync — never commit a real JWT key. Other JWT/CORS values live in `appsettings.Production.json` or render.yaml `envVars`. Render env vars use `__` (double underscore) for `:` nesting.
- Render terminates TLS at the proxy and forwards plain HTTP. Server `Program.cs` enables `UseForwardedHeaders` and only calls `UseHttpsRedirection` in Development.
- Custom domain via Cloudflare DNS, automatic Let's Encrypt TLS — added in the Render dashboard after first deploy.
- **SQLite caveat**: Render's filesystem is ephemeral; `demo.db` resets on every redeploy. Acceptable for a demo (auto-migrate seeds the schema on boot). For persistence, add a Render Disk and mount it at the path containing `demo.db`.

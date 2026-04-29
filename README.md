# AirrostiDemo

A small full-stack demo built on **.NET 8 + Blazor WebAssembly** that lets a
visitor look up real adverse-event data for common pain medications and
optionally save personalised reports behind authentication.

The project showcases an end-to-end stack — live external data, an
authenticated persistence layer, and a single-blueprint cloud deployment —
in a footprint small enough to read in an afternoon.

---

## What it does

1. **Browse adverse-event data anonymously.** The Drug Search page (`/drugs`
   in the client) lets you search for a drug or pick from a curated list
   (ibuprofen, naproxen, oxycodone, acetaminophen, tramadol, celecoxib).
   Results come live from the FDA's [OpenFDA adverse-event reporting
   system](https://open.fda.gov/apis/drug/event/), proxied through the
   server so the WASM client never hits FDA directly.

2. **Sign up / log in.** Clicking "Save my results" while anonymous opens a
   modal where the visitor can register or log in. Authentication is
   handled by ASP.NET Core Identity backed by EF Core + SQLite, with the
   server minting JSON Web Tokens for the client to store.

3. **Save and revisit reports.** Authenticated users can persist any FDA
   snapshot to their account and view their list at `/my-reports`.

---

## Solution layout

Three .NET 8 projects coordinated by `AirrostiDemo.sln`:

| Project | SDK | Purpose |
| --- | --- | --- |
| **AirrostiDemo** | `Microsoft.NET.Sdk.BlazorWebAssembly` | Standalone Blazor WASM client — the SPA the user sees. |
| **AirrostiDemo.Server** | `Microsoft.NET.Sdk.Web` | ASP.NET Core Web API. Hosts the FDA proxy, auth endpoints, and saved-report CRUD. |
| **AirrostiDemo.Shared** | Plain class library | DTOs referenced by both client and server — single source of truth for the wire format. |

The client and server are **decoupled**: the WASM client runs from a static
host and talks to the API over HTTPS as a separate origin. CORS on the
server is locked down to the known client origin(s).

```
┌─────────────────────┐                  ┌────────────────────────┐
│  Blazor WASM client │  HTTPS  ──────▶  │  ASP.NET Core Web API  │
│  (AirrostiDemo)     │                  │  (AirrostiDemo.Server) │
└─────────────────────┘                  │   ├── OpenFDA proxy    │
        │                                │   ├── Auth (JWT)       │
        ▼ localStorage                   │   └── Reports (EF/SQL) │
   token / expiry                        └────────────┬───────────┘
                                                      │
                                              ┌───────▼────────┐
                                              │   OpenFDA      │
                                              │ api.fda.gov    │
                                              └────────────────┘
```

---

## Prerequisites

You'll need:

- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** —
  required by every project. Verify with `dotnet --list-sdks`.
- **[git](https://git-scm.com/downloads)** — to pull the repo.
- *(Optional)* **Visual Studio 2022 17.8+** or **Rider 2023.3+** if you
  prefer an IDE; everything below also works from the command line in
  VS Code or any plain terminal.
- *(Optional)* A free **OpenFDA API key** from
  [open.fda.gov](https://open.fda.gov/apis/authentication/). Without one
  the demo still works, but FDA caps unauthenticated traffic at ~1,000
  requests/IP/day. With a key it lifts to ~120,000/day.

No database server is required — the server uses **SQLite** (a single
`demo.db` file alongside the binary).

---

## Setup from a fresh clone

```bash
# 1. Clone the repo
git clone <repo-url> AirrostiDemo
cd AirrostiDemo

# 2. Restore packages once
dotnet restore AirrostiDemo.sln

# 3. (One-time, dev only) trust the ASP.NET Core dev HTTPS cert
dotnet dev-certs https --trust
```

That's the whole setup. There's **no manual database step** — the server
runs `db.Database.Migrate()` on startup, so the SQLite file is created
and the schema applied automatically the first time you run the API.

---

## Running locally

The project requires **both** the client and the API to be running, in
separate terminals, because the WASM client talks to the server
cross-origin.

```bash
# Terminal 1 — start the API at https://localhost:7204
dotnet run --project AirrostiDemo.Server --launch-profile https

# Terminal 2 — start the WASM client at https://localhost:7168
dotnet run --project AirrostiDemo --launch-profile https
```

Then open **<https://localhost:7168>** and click **Try the Demo**.

Useful URLs while running:

| URL | What |
| --- | --- |
| `https://localhost:7168/` | Client — landing page |
| `https://localhost:7168/drugs` | Client — drug-search demo |
| `https://localhost:7168/my-reports` | Client — saved reports (auth required) |
| `https://localhost:7204/swagger` | Server — interactive API docs (Development only) |
| `https://localhost:7204/api/Health` | Server — liveness probe |

### If the URLs above don't match

Dev URLs are defined in `Properties/launchSettings.json` of each project.
If you change them you also need to update:

- `AirrostiDemo/wwwroot/appsettings.Development.json` →
  `ApiBaseAddress` (the URL the client calls).
- `AirrostiDemo.Server`'s CORS allow-list — defaults to
  `https://localhost:7168`; override via `Cors:AllowedOrigins` in
  `appsettings.Development.json` (comma-separated list).

---

## Configuration

All runtime config lives in standard ASP.NET Core `appsettings.*.json`
files. The keys you may want to touch:

### Client (`AirrostiDemo/wwwroot/appsettings.{Environment}.json`)

| Key | Purpose |
| --- | --- |
| `ApiBaseAddress` | Absolute URL of the API. Required. |

### Server (`AirrostiDemo.Server/appsettings.{Environment}.json`)

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | EF Core / SQLite connection string. Default: `Data Source=demo.db`. |
| `Jwt:Key` | HMAC-SHA256 signing key. Use a long random string in production. |
| `Jwt:Issuer` / `Jwt:Audience` | Identity values baked into the JWT. Must match validation. |
| `Jwt:ExpiresMinutes` | Token lifetime in minutes. Default 60. |
| `Cors:AllowedOrigins` | Comma-separated list of origins allowed to call the API. |
| `OpenFda:ApiKey` | *(Optional)* OpenFDA API key. Lifts the per-IP quota. |

In production those values come from environment variables instead of
`appsettings.json`. Render's env-var convention uses double underscores
to denote configuration nesting (e.g. `Jwt__Key`, `Cors__AllowedOrigins`).

---

## Database

- **Engine**: SQLite via EF Core 8 (`Microsoft.EntityFrameworkCore.Sqlite`).
- **File**: `demo.db`, created next to the server binary.
- **Migrations**: applied automatically at startup. To add a schema
  change yourself, run from the `AirrostiDemo.Server/` directory:
  ```bash
  dotnet ef migrations add <Name>
  dotnet ef database update   # optional — startup also applies it
  ```
- **Caveat**: SQLite cannot `ORDER BY` a `DateTimeOffset`. The
  `SavedReport.SavedAt` column is therefore a plain `DateTime` (UTC), and
  the `ReportsController` re-attaches `DateTimeKind.Utc` when projecting
  back to the DTO.

---

## Authentication overview

The auth flow is plain bearer-token JWT — no cookies, no external IdP:

1. The client `POST`s `/api/Auth/register` (or `/login`) with email +
   password.
2. The server verifies via ASP.NET Core Identity's `UserManager`.
3. On success, `JwtTokenService` mints an HMAC-SHA256-signed token whose
   `sub` claim is the user id.
4. The client persists the token + expiry in `localStorage`
   (`airrosti_token`, `airrosti_expires`).
5. `AuthHeaderHandler` (a `DelegatingHandler` slotted into the named
   "AirrostiApi" `HttpClient`) reads the stored token at request time and
   attaches `Authorization: Bearer <token>` to every outbound call.
6. Server endpoints decorated with `[Authorize]` validate the token's
   signature, issuer, audience, and expiry, and read the calling user
   id out of `ClaimTypes.NameIdentifier`. **The server never trusts a
   user id sent in the request body.**

---

## Project commands cheat sheet

Run from the repo root unless noted:

```bash
# Build the whole solution
dotnet build AirrostiDemo.sln

# Run the API (Swagger UI at https://localhost:7204/swagger in Development)
dotnet run --project AirrostiDemo.Server --launch-profile https

# Run the WASM client (the API must also be running)
dotnet run --project AirrostiDemo --launch-profile https

# EF Core migrations (run from AirrostiDemo.Server/)
dotnet ef migrations add <Name>
dotnet ef database update

# Restore / clean
dotnet restore AirrostiDemo.sln
dotnet clean AirrostiDemo.sln
```

There is currently no test project in the solution.

---

## Deployment

Target host: **[Render](https://render.com)**, configured by the
`render.yaml` blueprint at the repo root.

- **Server**: Docker web service built from `AirrostiDemo.Server/Dockerfile`
  (multi-stage, .NET 8 SDK → ASP.NET runtime). Listens on `:10000` (Render's
  default Docker port).
- **Client**: Render Static Site. Build command publishes `AirrostiDemo`
  and serves `publish/wwwroot`. SPA rewrite rule sends every path to
  `index.html` so deep links work.
- **Secrets**: `Jwt__Key` is generated by Render on first sync
  (`generateValue: true`); never commit a real signing key to source
  control.
- **HTTPS**: Render terminates TLS at its proxy and forwards plain HTTP.
  The server enables `UseForwardedHeaders` for that reason and only
  enables `UseHttpsRedirection` in Development.
- **SQLite caveat in production**: Render's filesystem is ephemeral, so
  `demo.db` resets on every redeploy. That's acceptable for the demo
  (auto-migrate seeds the schema on boot). For real persistence, attach
  a Render Disk and mount it at the path containing `demo.db`.

Push to `master` → Render auto-redeploys whichever service had its tracked
files changed.

---

## Further reading

- `CLAUDE.md` — repo orientation for AI assistants and humans.
- `docs/PLAN.md` — phased build plan.
- `docs/DECISIONS.md` — architectural rationale.

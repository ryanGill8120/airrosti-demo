# Architecture Decisions

Short log of *why* the project is built this way. Intended both as a reference
during development and as talking points for the demo interview.

## Hosting model: standalone WASM + separate Web API (not Blazor Hosted)
The .NET 8 templates removed the "ASP.NET Core Hosted" checkbox. Manually
splitting the projects is also a clearer architectural story for the demo —
mirrors the Angular + .NET API pattern from prior experience and makes the
client/server boundary explicit.

## Auth: ASP.NET Core Identity + JWT (not cookies, not external IdP)
- No external dependency, fully self-contained in the repo.
- JWT pattern matches what the audience would build for a SPA + API split.
- External IdPs (Entra, Auth0) add setup overhead disproportionate to a demo.
- Cookies would require shared-domain or careful CORS/SameSite configuration
  that doesn't pay off for a demo.

## Storage: SQLite via EF Core (not SQL Server, not Azure SQL)
- Single file, no server, travels with the deployment.
- EF Core migrations make the swap to SQL Server / Azure SQL a one-line
  connection-string change — good talking point.

## Hosting: Render (not Azure App Service)
- Azure F1 free tier does not support custom domains or HTTPS for custom
  domains; B1 (~$13/mo) is the minimum.
- Render's free tier includes custom domains + automatic Let's Encrypt TLS.
- $7/mo Starter tier removes cold starts for the live demo.
- Azure knowledge can still be discussed verbally without paying to host
  there.

## DNS: Cloudflare (not Render-managed, not registrar default)
- At-cost domain pricing.
- Anycast DNS propagates A/CNAME changes globally in minutes.

## OpenFDA called server-side (not directly from WASM)
- Keeps any future API key out of client bundles.
- Allows server-side caching/rate-limit handling later.
- Demonstrates correct "no third-party calls from the browser" hygiene.

# AirrostiDemo

A small .NET 8 + Blazor WebAssembly demo showcasing a SPA + Web API split.

## What it does today (Phase 1)

The **Drug Search** page (`/drugs` in the client) lets you look up real adverse-event
reaction counts for common pain medications, sourced live from the FDA's
[OpenFDA drug event API](https://open.fda.gov/apis/drug/event/).

The Blazor WASM client never hits FDA directly. It calls the local API at
`GET /api/DrugSideEffects/{drugName}`, which proxies the request through a
typed `OpenFdaClient` service on the server. The server-side
`IHttpClientFactory` named client `"openFDA"` owns the FDA base address and
makes the swap to a different upstream a one-line change.

Pre-wired demo drugs: ibuprofen, naproxen, oxycodone, acetaminophen, tramadol,
celecoxib.

## Run it

Both projects must be running together — the client (`https://localhost:7168`)
calls the server (`https://localhost:7204`) cross-origin via CORS.

```bash
# Terminal 1 — API
dotnet run --project AirrostiDemo.Server --launch-profile https

# Terminal 2 — WASM client
dotnet run --project AirrostiDemo --launch-profile https
```

Then open `https://localhost:7168/drugs` and click a drug.

## Further reading

- `CLAUDE.md` — repo orientation for AI assistants and humans alike.
- `docs/PLAN.md` — phased build plan.
- `docs/DECISIONS.md` — architectural rationale.

# AGENTS.md

## Cursor Cloud specific instructions

Botijas CO₂ is a CO₂ cylinder refill-management system with three runtime pieces:

- Backend API — ASP.NET Core (**.NET 10**), `src/Botijas.Api` (Clean Architecture: `Botijas.Domain` / `Botijas.Application` / `Botijas.Infrastructure` / `Botijas.Api`).
- Frontend web — Next.js 14 (App Router, TypeScript, Tailwind, next-intl PT-PT/EN), `web/`.
- PostgreSQL 16 database.

Standard dev/build/lint/test commands live in `README.md`, `QUICK_START.md`, `docs/07_runbook.md`, and `web/package.json`. The notes below are the non-obvious caveats for running it in this environment.

### Doc drift — trust the code, not the docs
`README.md` / `QUICK_START.md` / `docs/` say **SQLite** and **.NET 8**; the actual code uses **PostgreSQL (Npgsql)** on **.NET 10** (`src/Botijas.Api/Program.cs` calls `UseNpgsql` and auto-runs EF migrations on startup). SignalR and the `Botijas.PrintGateway` "print gateway" worker are **not implemented** (leftover references only) — ignore them. `@microsoft/signalr` in `web/package.json` is unused.

### Tooling locations
- .NET SDK is installed at `~/.dotnet` (added to `PATH` via `~/.bashrc`). If `dotnet` is not found in a non-login shell, use the full path `~/.dotnet/dotnet` or `source ~/.bashrc`.
- Node/npm are preinstalled system-wide.

### PostgreSQL is NOT auto-started on VM boot
Start it before running the API (data/user already provisioned: db `devdb`, user `devuser`, pass `devpass`):
```bash
sudo pg_ctlcluster 16 main start
```
The API waits/retries ~40s for the DB and applies all EF migrations automatically on startup, so no manual migration step is needed.

### Running the API on port 8080 (important)
The web app (dev) defaults to the API at `http://localhost:8080` (`web/lib/api.ts`), but `dotnet run` uses the `http` launch profile in `Properties/launchSettings.json` which forces port **5001** and **overrides `ASPNETCORE_URLS`**. To run on 8080, bypass the profile:
```bash
cd src/Botijas.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://0.0.0.0:8080
```

### Running the web app
```bash
cd web && npm run dev      # http://localhost:3000, redirects to /pt-PT
```

### Auth / credentials (dev)
- Frontend login is hardcoded client-side: username `oficina`, password `oficina`.
- API expects header `X-Api-Key: oficina` (config key `API_KEY`). In `Development` the key check is bypassed if `API_KEY` is unset, but the web app sends `oficina` by default (`NEXT_PUBLIC_API_KEY`).
- `POST /api/customers` requires a `phoneType` field of `PT` or `International` (not documented in the quick-start curl examples).

### appsettings.json points at a remote Neon DB
`src/Botijas.Api/appsettings.json` contains a hardcoded production Neon connection string. `appsettings.Development.json` (used when `ASPNETCORE_ENVIRONMENT=Development`) points at the local Postgres — always run the API in Development locally.

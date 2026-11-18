# optimizely-site

A minimal **Optimizely CMS 13** site on .NET 10 that doubles as the live diagnostics test bed for
Reignite's profiler/diagnostics services. It exists so the diagnostics stack can be
exercised against a real CMS host: pages that deliberately manifest server problems,
instrumented so the profiler can capture them and the Teams/Web Chat diagnostics bot can
surface them.

## Stack

- .NET 10 (`src/OptimizelySite`)
- Optimizely CMS 13.7.0
- Reignite.Optimizely.Counters.CMS (the Reignite counter packages)
- Azure SQL (`optimizely-lab` database)
- ASP.NET Core minimal-API endpoints for the fault lab (`src/OptimizelySite/Lab`)
- A standalone page preview at `/preview` (renders the views without CMS content routing)

## Seed safety (CMS 13)

`SiteSeedModule` creates the CMS 13 `Website` application and the content tree on an
**empty** database, and is deliberately **create-only**: if an application for this lab
already exists it is left untouched. Production and the lab share the same database, and
the seed runs unattended at startup, so it must never re-point an existing application's
hosts or entry point. The create path has not yet been exercised against a genuinely empty
database — the CMS 12 → 13 migration created the current application row.

## Run locally

The CMS needs a SQL Server connection string (Optimizely does not support PostgreSQL)
and, on `localhost`, no Optimizely license.

```bash
# .env.local holds the connection string; it is gitignored.
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__EPiServerDB="$(cat .env.local)" \
dotnet run --project src/OptimizelySite
```

Then open <http://localhost:8123/preview> (the standalone page preview) and
<http://localhost:8123/preview/lab>. The CMS-served routes require an Optimizely license
on a public host; on localhost they are license-exempt.

## Fault lab

Every fault endpoint is **off by default** and requires `Lab__Enabled=true` plus a matching
`Lab__Key` (`?key=…`). A deployment without a key fails closed (404).

| Endpoint | What it manifests |
| --- | --- |
| `GET /lab` | Index (no key) |
| `GET /lab/health` | Baseline (no key) |
| `GET /lab/memory?mb=256&hold=15` | Retained memory pressure |
| `GET /lab/cpu?seconds=10` | CPU burn |
| `GET /lab/threads?count=32&hold=10` | Thread-pool starvation |
| `GET /lab/alloc?mb=32&seconds=10` | Allocation / GC pressure |
| `GET /lab/exception` | Unhandled exception |
| `GET /lab/slow-sql?ms=5000` | Slow SQL against `EPiServerDB` |

All values are clamped to the maxima in the `Lab` configuration section
(`MaxMemoryMb`, `MaxCpuSeconds`, `MaxHoldSeconds`, `MaxSqlDelayMs`, `MaxThreads`).

## Configuration

| Key | Purpose |
| --- | --- |
| `ConnectionStrings__EPiServerDB` | Azure SQL connection string (App Service setting; never committed) |
| `Lab__Enabled` | Master switch for fault endpoints |
| `Lab__Key` | Shared key required by every fault endpoint |

## Security notes

- Secrets live in App Service settings / local environment, never in the repo.
- The fault endpoints are operator-only; the anonymous customer-facing conversation runs
  against the diagnostics bot via Web Chat, not through this site.

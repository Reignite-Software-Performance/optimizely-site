# optimizely-site

A minimal **Optimizely CMS 12** site that doubles as the live diagnostics test bed for
Reignite's profiler/diagnostics services. It exists so the diagnostics stack can be
exercised against a real CMS host: pages that deliberately manifest server problems,
instrumented so the profiler can capture them and the Teams/Web Chat diagnostics bot can
surface them.

## Stack

- .NET 8 (`src/OptimizelySite`)
- Optimizely CMS 12.35.0
- Azure SQL (`optimizely-lab` database)
- ASP.NET Core minimal-API endpoints for the fault lab (`src/OptimizelySite/Lab`)

## Run locally

The CMS needs a SQL Server connection string (Optimizely does not support PostgreSQL)
and, on `localhost`, no Optimizely license.

```bash
# .env.local holds the connection string; it is gitignored.
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__EPiServerDB="$(cat .env.local)" \
dotnet run --project src/OptimizelySite
```

Then open <http://localhost:8000/> (first run initializes the CMS schema; the site root
is empty until content is created) and <http://localhost:8000/lab>.

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

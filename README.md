# DepScan: open-source dependency risk intelligence

DepScan scans any public GitHub repository for vulnerable open-source dependencies. It finds every manifest (including the nested packages of a monorepo), matches each **exact dependency version** against [OSV.dev](https://osv.dev), flags CVEs that attackers are **actively exploiting** ([CISA KEV](https://www.cisa.gov/known-exploited-vulnerabilities-catalog)), ranks findings by risk, re-scans every repository daily, and pushes live alerts when a new advisory affects you.

![Dashboard](docs/screenshots/dashboard-dark.png)

| | |
|---|---|
| ![Repository report](docs/screenshots/detail-dark.png) | ![Light theme](docs/screenshots/dashboard-light.png) |

## Highlights

- **8 ecosystems, 12 manifest formats.** npm, PyPI, Maven and Gradle, NuGet (including Central Package Management), RubyGems, Go, Packagist and crates.io. Formats: `package.json`, `requirements*.txt`, `pyproject.toml` (PEP 621, PEP 735 and Poetry), `pom.xml` (with property and `dependencyManagement` resolution), `build.gradle(.kts)`, `*.csproj`, `Directory.Packages.props`, `Gemfile`, `go.mod`, `composer.json` and `Cargo.toml`.
- **Version-accurate matching.** Constraints such as `^4.17.1` or `>=2.0,<3` are normalised to a concrete version and checked through OSV's batch API in one request per scan, so findings are real matches rather than keyword hits.
- **Risk-based prioritisation.** CVSS v3.x base scores are computed from vector strings, combined with severity and KEV exploitation into a 0–100 risk score, then rolled up into an A–F repository grade.
- **Actionable output.** A "Fix first" upgrade plan deduplicated per package, a CSV export, and a **CycloneDX 1.5 SBOM** with embedded vulnerability data.
- **Continuous monitoring.** A daily background re-scan, history snapshots, a 30-day portfolio trend, and SignalR push notifications scoped to the signed-in user.
- **Production-minded.** Per-user data isolation, cookie auth with rate limiting, ProblemDetails errors, health checks, Data Protection keys persisted to Postgres, and a single Docker image.

## Architecture

```mermaid
flowchart LR
  subgraph Browser
    SPA["React 19 SPA<br/>TanStack Query · Tailwind v4 · Recharts"]
  end

  subgraph Container["ASP.NET Core 10 (single container)"]
    API["REST API<br/>Controllers · ProblemDetails · Rate limiting"]
    HUB["SignalR hub<br/>per-user alerts"]
    JOBS["Hangfire workers<br/>scan jobs · daily re-scan"]
    PIPE["Analysis pipeline<br/>discover → parse → normalise → match → score"]
  end

  PG[("PostgreSQL<br/>EF Core · Hangfire · DP keys")]
  REDIS[("Redis (optional)<br/>advisory & blob cache")]
  GH["GitHub<br/>REST trees API + raw content"]
  OSV["OSV.dev<br/>batch query API"]
  KEV["CISA KEV feed"]

  SPA -- "HTTPS, same origin" --> API
  SPA <-- WebSocket --> HUB
  API -- enqueue --> JOBS
  JOBS --> PIPE
  PIPE --> GH & OSV & KEV
  PIPE --> PG
  PIPE -- alerts --> HUB
  API --> PG
  PIPE <--> REDIS
```

**How a scan works**

1. `POST /api/repositories` validates the URL (HTTPS, SSH and `owner/repo` forms are accepted), creates the record and enqueues a Hangfire job.
2. The job reads repository metadata, then lists the git tree to discover manifests. Vendored, fixture and sample directories are skipped, and the job falls back to probing the repository root when the GitHub API quota runs out.
3. Manifests are fetched from `raw.githubusercontent.com`, which doesn't count against the API quota, and cached by blob SHA. Each manifest is parsed by its format's parser; one malformed manifest is reported without failing the whole scan.
4. Dependencies are deduplicated across manifests and their versions normalised. All packages then go to OSV in **one batch request**. Advisory details are fetched in parallel and cached by modification time.
5. Each finding is scored, exploitation is flagged from KEV, and the results replace the previous scan in a single unit of work. A snapshot is recorded, and SignalR pushes progress and completion to the owner. On a re-scan, the owner is alerted only about vulnerabilities that are new.

## Risk model

| Level | Formula |
|---|---|
| Finding (0–100) | severity weight (Low 5, Medium 15, High 25, Critical 40) + CVSS × 5 + 10 if exploited (KEV). A finding without a CVSS vector uses its severity band's typical score. |
| Repository (0–100) | `R = r₁ + (100 − r₁) · (1 − Π(1 − rᵢ/200))`. The worst finding sets the floor and each additional finding closes part of the remaining gap. Unlike an average, one critical can't be diluted by many lows; unlike a sum, the score stays bounded. |
| Grade | A = 0, B < 30, C < 50, D < 75, F ≥ 75. The thresholds line up with single-finding scores for each severity. |

## Testing

| Suite | Tests | Line coverage | Branch coverage |
|---|---|---|---|
| Backend (xUnit · NSubstitute · WebApplicationFactory) | **326** | **96.5%** | **87.3%** |
| Frontend (Vitest · React Testing Library) | **107** | **96.9%** | **88.4%** |

Backend coverage excludes only generated EF migrations. The backend suite has three layers:

- **Unit tests.** Every parser, the CVSS calculator (checked against published reference scores), version normalisation, risk scoring, purl and SBOM generation.
- **Integration tests.** OSV, GitHub, KEV and the retry handler run against a stubbed HTTP transport, and the analysis pipeline runs against EF Core.
- **API tests.** These boot the real ASP.NET pipeline: auth, per-user isolation, rate limiting, validation, conflicts, health and OpenAPI. One end-to-end test drives *HTTP request → job → GitHub/OSV/KEV → database → SBOM*.

No Docker is needed to run any of the tests.

```bash
# Backend
dotnet test DepScan.slnx --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet tool restore && dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/coverage -reporttypes:"Html;TextSummary"

# Frontend
cd oss-analyser-frontend && npm ci && npm run test:coverage
```

GitHub Actions (`.github/workflows/ci.yml`) builds with warnings as errors, runs both suites with coverage summaries, audits production npm dependencies and builds the Docker image.

## Run locally

**Option A: Docker (everything).**

```bash
cp .env.example .env          # optionally add GITHUB_TOKEN
docker compose up --build     # http://localhost:8080
```

**Option B: hot-reload development.** Requires the .NET 10 SDK, Node 22+ and a PostgreSQL instance. Redis is optional; without it the app uses an in-memory cache.

```bash
docker compose up -d postgres redis            # or use your own Postgres on :5433
dotnet run --project OSSDependencyAnalyser.API # API on http://localhost:5100
cd oss-analyser-frontend && npm ci && npm run dev   # UI on http://localhost:5173
```

Vite proxies `/api` and `/hubs` to the API, so development is same-origin just like production. The interactive API reference is at `/scalar` and the OpenAPI document at `/openapi/v1.json`.

## Deploy

The root `Dockerfile` produces one image that serves both the API and the built SPA. It runs as a non-root user and listens on `$PORT`.

### Render (one click, free tier)

1. Push this repository to GitHub.
2. In Render: **New → Blueprint**, select the repository, then **Apply**. `render.yaml` provisions Postgres and the web service and wires `DATABASE_URL` between them.
3. Optional: in the service's **Environment** tab, set `GithubApi__Token` to a GitHub token with no scopes. This raises the GitHub API quota from 60 to 5,000 requests per hour.

Migrations run automatically on startup. Note that free Render services sleep when idle, so the daily re-scan only runs while the service is awake. Use a paid instance, or an uptime pinger hitting `/health/live`, for continuous monitoring.

### Any other container host (Railway, Fly.io, Azure Container Apps, Cloud Run, a VM)

Build the image and supply a Postgres connection:

| Variable | Required | Purpose |
|---|---|---|
| `DATABASE_URL` *or* `ConnectionStrings__DefaultConnection` | ✅ | Postgres. URI form (`postgres://user:pass@host/db`) and Npgsql key/value form both work. |
| `ConnectionStrings__Redis` | | Shared cache across instances (defaults to in-memory). |
| `GithubApi__Token` | | Higher GitHub API quota. |
| `Hangfire__WorkerCount` | | Concurrent scans per instance (default 4). |
| `Auth__CrossSiteCookies` | | Set to `true` only if the SPA is hosted on a different site than the API (with `Cors__AllowedOrigins__0`). |
| `ApiDocs__Enabled` | | Set to `false` to hide `/scalar` and `/openapi`. |

Health checks: `/health/live` (process) and `/health/ready` (database).

## API

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/auth/register` · `/login` · `/logout` | Cookie-based authentication (rate-limited) |
| `GET` | `/api/auth/me` | Current user |
| `GET` | `/api/dashboard` | Portfolio KPIs, severity mix, ecosystems, riskiest repos, top packages, 30-day trend |
| `GET` · `POST` | `/api/repositories` | List (with `?search=` and `&status=`) and queue a scan |
| `GET` · `DELETE` | `/api/repositories/{id}` | Repository summary, or remove it |
| `POST` | `/api/repositories/{id}/rescan` | Re-run the analysis (409 if one is already running) |
| `GET` | `/api/repositories/{id}/vulnerabilities` · `/dependencies` · `/snapshots` | Findings, inventory and history |
| `GET` | `/api/repositories/{id}/sbom` | CycloneDX 1.5 SBOM download |
| WS | `/hubs/alerts` | SignalR: progress, completion, failure and new-vulnerability events |

## Project layout

```
OSSDependencyAnalyser.API/       ASP.NET Core API
  Controllers/                   Auth, Repositories, Dashboard
  Domain/                        GitHub URL parsing, CVSS calculator, version normaliser, purl
  Integrations/                  GitHub, OSV, CISA KEV clients · manifest parsers · retry handler
  Services/                      Analysis pipeline, risk scoring, snapshots, alerts, SBOM
  Infrastructure/                DI composition, rate-limit policies, DATABASE_URL support
OSSDependencyAnalyzer.Tests/     xUnit unit, integration and API tests
oss-analyser-frontend/           React 19 + TypeScript SPA (Vite, Tailwind v4, TanStack Query)
Dockerfile · docker-compose.yml · render.yaml · .github/workflows/ci.yml
```

## Design notes and trade-offs

- **No lockfile resolution yet.** Without a lockfile, a constraint is matched at its *lower bound*, the oldest version it permits. This is conservative: a range may be reported vulnerable even if the installed version has the fix. Lockfile parsing (`package-lock.json`, `poetry.lock`) is the natural next step.
- **Transitive dependencies are out of scope.** Go's `// indirect` requirements are included because `go.mod` lists them; other ecosystems report direct dependencies only.
- **Same-origin by default.** Serving the SPA from the API avoids CORS and third-party-cookie problems in modern browsers. A split deployment is still possible via `Auth__CrossSiteCookies`.

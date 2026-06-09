# CLAUDE.md

Guidance for Claude Code (and humans) working in this repo.

## What this is

A **proof-of-concept for unified observability**: traces, logs, metrics and
Discord alerting for multiple apps, sharing one Grafana stack. Two example
projects emit telemetry via OpenTelemetry:

- **`poc-telemetry`** — .NET 9 Clean Architecture: `api` (ASP.NET Core), `worker`
  (MassTransit/RabbitMQ consumer), `web` (Angular, served by nginx), `mysql`,
  `rabbitmq`.
- **`demo-python`** — a small FastAPI service (`src-py/`).

## Architecture — two Docker Compose stacks

| Stack | File | Services |
|---|---|---|
| **Observability** (shared) | `observability/docker-compose.yml` | otel-collector, Tempo, Loki, Mimir, Grafana |
| **App** | `docker-compose.yml` (project name `poc-telemetry`) | api, worker, web, mysql, rabbitmq, demo-python |

App services join two networks: the project `default` **and** the external
`observability` network, so they can reach the collector. Telemetry flows:
apps → **OTel Collector** (OTLP) → Tempo (traces) / Loki (logs) / Mimir (metrics);
Tempo's metrics-generator also produces span metrics into Mimir.

## First-time setup on a new machine

Order matters. The app stack depends on the external network and the
observability stack.

```bash
# 1. One-time: create the shared network
docker network create observability

# 2. Discord webhook (NOT in git — see Gotchas)
cp observability/.env.example observability/.env
#    then edit observability/.env and paste the real DISCORD_WEBHOOK_URL

# 3. Start the observability stack
docker compose -f observability/docker-compose.yml up -d

# 4. Build + start the app stack (first build is slow: .NET + Angular + Python)
docker compose up --build -d
```

> The very first `--build` pulls base images and compiles three apps — can take
> several minutes. **Do this well before any demo, not live.**

### One-command preflight

After step 1–2, a preflight script starts both stacks and verifies every service
(and auto-fixes the nginx 502 below):

```powershell
.\preflight.ps1          # Windows; add -Build on the first run
```
```bash
./preflight.sh           # macOS/Linux; add --build on the first run
```

## URLs

| What | URL | Notes |
|---|---|---|
| Angular app | http://localhost:4200 | nginx proxies `/api` → `api:8080` |
| API | http://localhost:5000/api/products | |
| demo-python | http://localhost:5001/ | `/health`, `/metrics`, `/errors/*` |
| Grafana | http://localhost:3000 | anonymous Admin (also admin/admin) |
| RabbitMQ UI | http://localhost:15672 | guest/guest |
| Mimir / Loki / Tempo | :9009 / :3100 / :3200 | internal datasources |

## Observability & alerting specifics

- **`project` label** isolates each app. Set via `OTEL_RESOURCE_ATTRIBUTES`
  (`project=poc-telemetry` / `project=demo-python`); Grafana dashboards have a
  `$project` selector.
- **Everything is provisioned as files** under `observability/grafana/provisioning/`:
  datasources, dashboards (JSON), and alerting (`alerting/*.yaml`).
- **Alerting → Discord.** Three rules (`observability/grafana/provisioning/alerting/rules.yaml`):
  - `high-error-rate` — global error rate > 5% (`traces_spanmetrics_calls_total`)
  - `high-latency-p95` — p95 > 1s per service (`traces_spanmetrics_latency_bucket`)
  - `service-down` — `up == 0` (Prometheus scrape of each service's `/metrics`)
  Contact point + FIRING/RESOLVED templates + routing policy are in the same folder.
- The collector scrapes each service's `/metrics` via `docker_sd` using the
  `telemetry.scrape` label (see `observability/otel/otel-collector-config.yaml`).

## Gotchas (learned the hard way — keep these in mind)

- **`observability/.env` is gitignored** (holds the Discord webhook secret). After
  cloning on a new machine, recreate it from `.env.example` or Discord alerting
  notifications won't be sent.
- **502 Bad Gateway on localhost:4200** after recreating/rebuilding `api`: nginx in
  the `web` container caches the api's IP at startup. Fix: `docker restart
  poc-telemetry-web-1`, then check `curl http://localhost:4200/api/products`.
- **`docker_sd` multi-network duplicate targets**: containers are on two networks,
  so discovery creates one scrape target per (network, port). Only the
  `observability`-network target is routable from the collector — the others sit at
  `up=0` and would keep `service-down` firing. Fixed via a `keep` relabel on
  `__meta_docker_network_name` (RabbitMQ also filters port `15692`).
- **`docker stop` does not trigger `service-down`**: a stopped container disappears
  from discovery → `up` goes absent (not 0). To demo a down service, use
  `docker pause poc-telemetry-worker-1` / `docker unpause ...`.
- **Span status ≠ HTTP code**: an exception after the response can produce a span
  marked error with HTTP 201. Error-trace queries filter on
  `http.response.status_code >= 400`, not OTel status.

## Demo & presentation

- `docs/presentation.html` — reveal.js deck (~10 min, FR). Press **S** for speaker
  notes/timer; **O** for overview.
- `docs/demo-script.md` — step-by-step live-demo runbook (pre-arm the alert so it
  fires during the demo, trace→logs drill-down, multi-project compare, plan B).

## Repo layout

```
src/               # .NET solution (Domain, Application, Infrastructure, Api, Worker)
src-py/            # demo-python FastAPI service
web/               # Angular app (nginx)
observability/     # shared telemetry stack + Grafana provisioning + otel config
docs/              # architecture, presentation, demo runbook
docker-compose.yml # app stack (project: poc-telemetry)
```

# Setup overview

Vue d'ensemble: deux projets applicatifs (.NET et Python) partagent une seule stack d'observabilité avec dashboards filtrables par projet.

```mermaid
flowchart TB
    USER([👤 User / Browser])

    subgraph P1["🔵 Project: poc-telemetry (.NET)"]
        WEB["web<br/>Angular + nginx<br/>:4200"]
        API["api (.NET 9) :5000<br/>OTEL_SERVICE_NAME=api<br/>project=poc-telemetry"]
        WORKER["worker (.NET 9)<br/>OTEL_SERVICE_NAME=worker"]
        MYSQL[("mysql 8.4 :3306")]
        RMQ{{"rabbitmq 3.13<br/>:5672 / :15672"}}
    end

    subgraph P2["🟣 Project: demo-python"]
        PYAPI["demo-python (FastAPI) :5001<br/>OTEL_SERVICE_NAME=demo-python<br/>project=demo-python<br/>auto-instrumented via<br/>opentelemetry-instrument"]
    end

    subgraph ObsNet["🟢 Observability stack (shared)"]
        OTEL["otel-collector<br/>:4317 (gRPC) / :4318 (HTTP)"]
        TEMPO["tempo 2.6 :3200<br/>+ metrics-generator<br/>(span metrics + service graph)<br/>label: project"]
        LOKI["loki 3.2 :3100<br/>derivedFields:<br/>• View trace → Tempo<br/>• Back to Error Traces"]
        MIMIR["mimir 2.14 :9009<br/>traces_spanmetrics_*{project}<br/>traces_service_graph_*{project}"]
        GRAF["grafana 11.3 :3000<br/>traceQLStreaming = DISABLED"]
    end

    subgraph Dash["📊 Dashboards (filtered by $project)"]
        D1["Traces Overview<br/>uid: traces-overview<br/>• RED metrics<br/>• Latency heatmap<br/>• Recent traces<br/>• Service graph"]
        D2["Error Traces<br/>uid: error-traces<br/>• Error stats + rate<br/>• Top error spans<br/>• Error latency heatmap<br/>• http.status >= 400 table"]
    end

    %% Application flows
    USER ==>|HTTP :4200| WEB
    WEB ==>|/api proxy| API
    API ==>|EF Core SQL| MYSQL
    API ==>|publish| RMQ
    RMQ ==>|consume| WORKER

    USER ==>|HTTP :5001| PYAPI

    %% Telemetry exports
    API -.->|OTLP gRPC| OTEL
    WORKER -.->|OTLP gRPC| OTEL
    RMQ -.->|Prometheus scrape| OTEL
    PYAPI -.->|OTLP gRPC| OTEL

    %% Collector fan-out
    OTEL -.traces.-> TEMPO
    OTEL -.logs.-> LOKI
    OTEL -.metrics.-> MIMIR

    %% Tempo metrics-generator
    TEMPO ==>|remote_write<br/>span+service graph| MIMIR

    %% Grafana data
    GRAF -->|datasource| TEMPO
    GRAF -->|datasource| LOKI
    GRAF -->|datasource| MIMIR

    GRAF --- D1
    GRAF --- D2

    USER ==>|view :3000| GRAF

    %% Cross-signal correlation
    TEMPO -. tracesToLogsV2<br/>filter trace_id .-> LOKI
    LOKI -. derivedField TraceID .-> TEMPO
    LOKI -. derivedField ErrorDashboard .-> D2

    classDef app fill:#1f6feb,stroke:#0d419d,color:#fff
    classDef pyapp fill:#8957e5,stroke:#553098,color:#fff
    classDef data fill:#6f42c1,stroke:#4a2d85,color:#fff
    classDef obs fill:#1a7f37,stroke:#0d5226,color:#fff
    classDef dash fill:#bf8700,stroke:#7d5700,color:#fff
    class WEB,API,WORKER app
    class PYAPI pyapp
    class MYSQL,RMQ data
    class OTEL,TEMPO,LOKI,MIMIR,GRAF obs
    class D1,D2 dash
```

## Légende des flux

| Style          | Signification                                      |
|----------------|----------------------------------------------------|
| ══════►        | Trafic applicatif (HTTP, SQL, AMQP)                |
| ─ ─ ─►         | Export de télémétrie (OTLP, scrape, remote_write)  |
| ─ ─ · ─►       | Corrélations cross-signaux (clic / lien)           |

## Projets actifs

| Projet           | Stack             | Port | Service name(s)        | Attribut OTel              |
|------------------|-------------------|------|------------------------|----------------------------|
| `poc-telemetry`  | .NET 9 + Angular  | 4200 (UI) / 5000 (API) | `api`, `worker`     | `project=poc-telemetry`    |
| `demo-python`    | Python 3.11 / FastAPI | 5001 | `demo-python`     | `project=demo-python`      |

Les deux projets envoient leur télémétrie au **même otel-collector**, donc une seule chaîne Tempo/Loki/Mimir/Grafana sert les deux. Le filtre `$project` dans les dashboards permet de basculer ou de comparer.

## Comment afficher un projet dans les dashboards

1. Ouvrir un dashboard (`/d/traces-overview` ou `/d/error-traces`).
2. Sélecteur **Project** en haut → cocher un projet, plusieurs, ou `All`.
3. Le label `project` est dynamique: un projet n'apparaît que s'il a des spans dans les ~5 dernières minutes (sinon stale).

## Récap des modifs faites

### Fichiers créés
- `src-py/main.py`, `src-py/requirements.txt`, `src-py/Dockerfile` — service FastAPI.
- `observability/grafana/provisioning/dashboards/json/error-traces.json` — dashboard erreurs.
- `docs/architecture.md`, `docs/setup-overview.md` — diagrammes.

### Fichiers modifiés
- `docker-compose.yml` — ajout du service `demo-python`.
- `observability/docker-compose.yml` — `GF_FEATURE_TOGGLES_DISABLE: traceQLStreaming`.
- `observability/grafana/provisioning/datasources/datasources.yaml` — derivedField "Back to Error Traces".
- `observability/grafana/provisioning/dashboards/json/traces-overview.json` — heatmap, fix table TraceQL et service graph.

## Limites connues

- **Angular non instrumenté** → apparaît comme `user` dans le service graph (pas un vrai service).
- **MySQL invisible** dans le service graph (manque `peer_attributes` dans `tempo.yaml`).
- **Staleness** des `traces_spanmetrics_*`: un projet sans trafic récent (>5 min) disparaît du sélecteur jusqu'à génération de nouveau trafic.

# Architecture

```mermaid
flowchart LR
    USER([User / Browser])

    subgraph P1["Project: poc-telemetry (.NET)"]
        WEB[web<br/>Angular + nginx<br/>:4200]
        API[api<br/>.NET 9<br/>:5000]
        WORKER[worker<br/>.NET 9]
        MYSQL[(mysql 8.4<br/>:3306)]
        RMQ{{rabbitmq 3.13<br/>:5672 / :15672}}
    end

    subgraph P2["Project: demo-python"]
        PYAPI[demo-python<br/>FastAPI / Python 3.11<br/>:5001]
    end

    subgraph Obs["Observability stack (shared)"]
        OTEL[otel-collector<br/>:4317/:4318]
        TEMPO[(tempo 2.6<br/>traces<br/>:3200)]
        LOKI[(loki 3.2<br/>logs<br/>:3100)]
        MIMIR[(mimir 2.14<br/>metrics<br/>:9009)]
        GRAF[grafana 11.3<br/>:3000]
    end

    USER -->|HTTP :4200| WEB
    WEB -->|/api proxy| API
    API -->|SQL| MYSQL
    API -->|publish| RMQ
    RMQ -->|consume| WORKER

    USER -->|HTTP :5001| PYAPI

    API -.OTLP gRPC.-> OTEL
    WORKER -.OTLP gRPC.-> OTEL
    RMQ -.Prometheus scrape.-> OTEL
    PYAPI -.OTLP gRPC.-> OTEL

    OTEL -.traces.-> TEMPO
    OTEL -.logs.-> LOKI
    OTEL -.metrics.-> MIMIR
    TEMPO -.span metrics<br/>+ service graph.-> MIMIR

    GRAF --> TEMPO
    GRAF --> LOKI
    GRAF --> MIMIR
    USER -->|view dashboards| GRAF

    classDef app fill:#1f6feb,stroke:#0d419d,color:#fff
    classDef pyapp fill:#8957e5,stroke:#553098,color:#fff
    classDef data fill:#6f42c1,stroke:#4a2d85,color:#fff
    classDef obs fill:#1a7f37,stroke:#0d5226,color:#fff
    class WEB,API,WORKER app
    class PYAPI pyapp
    class MYSQL,RMQ data
    class OTEL,TEMPO,LOKI,MIMIR,GRAF obs
```

## Flux

- **Applicatif** (traits pleins):
  - Project `poc-telemetry`: `User → web (Angular) → api (.NET) → MySQL`, et `api → RabbitMQ → worker`.
  - Project `demo-python`: `User → demo-python (FastAPI)` directement.
- **Télémétrie** (traits pointillés): tous les services (.NET + Python) exportent en OTLP/gRPC vers le **même** otel-collector, qui répartit traces → Tempo, logs → Loki, metrics → Mimir. Tempo génère en plus des span-metrics et le service-graph qu'il pousse vers Mimir.
- **Grafana** lit les trois datasources et expose les dashboards avec un filtre `$project` pour basculer entre les projets ou les comparer.

## Projets

| Projet           | Stack                  | Ports exposés         | Attribut OTel              |
|------------------|------------------------|-----------------------|----------------------------|
| `poc-telemetry`  | .NET 9 + Angular       | 4200 (UI), 5000 (API) | `project=poc-telemetry`    |
| `demo-python`    | Python 3.11 / FastAPI  | 5001                  | `project=demo-python`      |

## Réseaux Docker

- `default` (par app stack).
- `observability` (externe, partagé): otel-collector, tempo, loki, mimir, grafana. Les services applicatifs sont attachés à `observability` pour atteindre le collector.

## Ports exposés (host)

| Service       | Port  | Usage                  |
|---------------|-------|------------------------|
| web           | 4200  | UI Angular             |
| api           | 5000  | API REST .NET          |
| demo-python   | 5001  | API REST FastAPI       |
| mysql         | 3306  | DB                     |
| rabbitmq      | 5672  | AMQP                   |
| rabbitmq      | 15672 | Management UI          |
| otel          | 4317  | OTLP gRPC              |
| otel          | 4318  | OTLP HTTP              |
| tempo         | 3200  | Tempo HTTP API         |
| loki          | 3100  | Loki HTTP API          |
| mimir         | 9009  | Mimir / Prometheus API |
| grafana       | 3000  | Grafana UI             |

# Architecture

```mermaid
flowchart LR
    subgraph App["Application (poc-telemetry network)"]
        WEB[web<br/>Angular + nginx<br/>:4200]
        API[api<br/>.NET 9<br/>:5000]
        WORKER[worker<br/>.NET 9]
        MYSQL[(mysql 8.4<br/>:3306)]
        RMQ{{rabbitmq 3.13<br/>:5672 / :15672}}
    end

    subgraph Obs["Observability stack (observability network)"]
        OTEL[otel-collector<br/>:4317/:4318]
        TEMPO[(tempo 2.6<br/>traces<br/>:3200)]
        LOKI[(loki 3.2<br/>logs<br/>:3100)]
        MIMIR[(mimir 2.14<br/>metrics<br/>:9009)]
        GRAF[grafana 11.3<br/>:3000]
    end

    USER([User / Browser]) -->|HTTP| WEB
    WEB -->|/api proxy| API
    API -->|SQL| MYSQL
    API -->|publish| RMQ
    RMQ -->|consume| WORKER

    API -.OTLP gRPC.-> OTEL
    WORKER -.OTLP gRPC.-> OTEL
    RMQ -.Prometheus scrape.-> OTEL

    OTEL -.traces.-> TEMPO
    OTEL -.logs.-> LOKI
    OTEL -.metrics.-> MIMIR
    TEMPO -.span metrics<br/>+ service graph.-> MIMIR

    GRAF --> TEMPO
    GRAF --> LOKI
    GRAF --> MIMIR
    USER -->|view dashboards| GRAF

    classDef app fill:#1f6feb,stroke:#0d419d,color:#fff
    classDef data fill:#8957e5,stroke:#553098,color:#fff
    classDef obs fill:#1a7f37,stroke:#0d5226,color:#fff
    class WEB,API,WORKER app
    class MYSQL,RMQ data
    class OTEL,TEMPO,LOKI,MIMIR,GRAF obs
```

## Flux

- **Applicatif** (traits pleins): `User → web (Angular) → api (.NET) → MySQL`, et `api → RabbitMQ → worker`.
- **Télémétrie** (traits pointillés): `api` et `worker` exportent en OTLP/gRPC vers `otel-collector`, qui répartit traces → Tempo, logs → Loki, metrics → Mimir. Tempo génère en plus des span-metrics et le service-graph qu'il pousse vers Mimir.
- **Grafana** lit les trois datasources pour ses dashboards.

## Réseaux Docker

- `default` (applicatif): web, api, worker, mysql, rabbitmq.
- `observability` (externe, partagé): otel-collector, tempo, loki, mimir, grafana. RabbitMQ et l'API sont aussi attachés à ce réseau pour atteindre le collector et exposer les métriques.

## Ports exposés (host)

| Service     | Port  | Usage                  |
|-------------|-------|------------------------|
| web         | 4200  | UI Angular             |
| api         | 5000  | API REST               |
| mysql       | 3306  | DB                     |
| rabbitmq    | 5672  | AMQP                   |
| rabbitmq    | 15672 | Management UI          |
| otel        | 4317  | OTLP gRPC              |
| otel        | 4318  | OTLP HTTP              |
| tempo       | 3200  | Tempo HTTP API         |
| loki        | 3100  | Loki HTTP API          |
| mimir       | 9009  | Mimir / Prometheus API |
| grafana     | 3000  | Grafana UI             |

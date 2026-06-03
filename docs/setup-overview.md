# Setup overview

Vue d'ensemble de tout ce qui a été mis en place: application, pipeline de télémétrie, génération de métriques, dashboards et corrélations cross-signaux.

```mermaid
flowchart TB
    USER([👤 User / Browser])

    subgraph AppNet["🔵 Application network (poc-telemetry)"]
        WEB["web<br/>Angular + nginx<br/>:4200<br/>— boutons simulate errors —"]
        API["api (.NET 9) :5000<br/>OTEL_SERVICE_NAME=api<br/>project=poc-telemetry<br/>controllers: Errors/throw,<br/>notfound, badrequest, slow"]
        WORKER["worker (.NET 9)<br/>OTEL_SERVICE_NAME=worker<br/>RabbitMQ consumer"]
        MYSQL[("mysql 8.4<br/>:3306")]
        RMQ{{"rabbitmq 3.13<br/>:5672 / :15672"}}
    end

    subgraph ObsNet["🟢 Observability network (external)"]
        OTEL["otel-collector<br/>:4317 (gRPC) / :4318 (HTTP)"]
        TEMPO["tempo 2.6 :3200<br/>+ metrics-generator<br/>(span metrics + service graph)"]
        LOKI["loki 3.2 :3100<br/>derivedFields:<br/>• View trace → Tempo<br/>• Back to Error Traces"]
        MIMIR["mimir 2.14 :9009<br/>traces_spanmetrics_*<br/>traces_service_graph_*"]
        GRAF["grafana 11.3 :3000<br/>traceQLStreaming = DISABLED<br/>(évite http2 frame too large)"]
    end

    subgraph Dash["📊 Dashboards provisioned"]
        D1["Traces Overview<br/>(uid: traces-overview)<br/>• RED metrics<br/>• Latency heatmap<br/>• Recent traces (TraceQL)<br/>• Service graph (Tempo serviceMap)"]
        D2["Error Traces<br/>(uid: error-traces)<br/>• Stat: error count + %<br/>• Top error spans<br/>• Error latency heatmap<br/>• Table http.status >= 400<br/>• Links → Loki + Overview"]
    end

    %% Application flow
    USER ==>|HTTP :4200| WEB
    WEB ==>|/api proxy| API
    API ==>|EF Core SQL| MYSQL
    API ==>|publish| RMQ
    RMQ ==>|consume| WORKER

    %% Telemetry export
    API -.->|OTLP gRPC<br/>traces+logs+metrics| OTEL
    WORKER -.->|OTLP gRPC<br/>traces+logs+metrics| OTEL
    RMQ -.->|Prometheus scrape| OTEL

    %% Collector fan-out
    OTEL -.traces.-> TEMPO
    OTEL -.logs.-> LOKI
    OTEL -.metrics.-> MIMIR

    %% Tempo metrics-generator
    TEMPO ==>|remote_write<br/>span+service graph<br/>label project| MIMIR

    %% Grafana data
    GRAF -->|datasource| TEMPO
    GRAF -->|datasource| LOKI
    GRAF -->|datasource| MIMIR

    %% Dashboard wiring
    GRAF --- D1
    GRAF --- D2

    USER ==>|view :3000| GRAF

    %% Cross-signal correlation (configured in datasources.yaml)
    D2 -. click traceID .-> TEMPO
    TEMPO -. tracesToLogsV2<br/>filter trace_id .-> LOKI
    LOKI -. derivedField TraceID .-> TEMPO
    LOKI -. derivedField ErrorDashboard .-> D2

    classDef app fill:#1f6feb,stroke:#0d419d,color:#fff
    classDef data fill:#8957e5,stroke:#553098,color:#fff
    classDef obs fill:#1a7f37,stroke:#0d5226,color:#fff
    classDef dash fill:#bf8700,stroke:#7d5700,color:#fff
    class WEB,API,WORKER app
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

## Récap des modifs faites dans cette session

### Fichiers modifiés
- `observability/docker-compose.yml` → désactivation du feature toggle `traceQLStreaming` sur Grafana.
- `observability/grafana/provisioning/datasources/datasources.yaml` → second `derivedField` Loki vers le dashboard Error Traces.
- `observability/grafana/provisioning/dashboards/json/traces-overview.json` → heatmap latence, tolérance noms de métriques, service graph via Tempo, fix table TraceQL.

### Fichiers créés
- `observability/grafana/provisioning/dashboards/json/error-traces.json` → dashboard dédié erreurs (uid `error-traces`).
- `docs/architecture.md` → diagramme architecture.
- `docs/setup-overview.md` → ce document.

## Problèmes résolus en cours de route

1. **`http2: frame too large`** sur la table de traces → causé par le streaming gRPC TraceQL activé par défaut en Grafana 11.x alors que le datasource pointe sur le port HTTP de Tempo. Fix: `GF_FEATURE_TOGGLES_DISABLE: traceQLStreaming`.
2. **Service graph vide** → le panneau nodeGraph attend un format spécifique (nodes + edges). Une requête Prometheus brute ne le produit pas. Fix: utiliser le datasource Tempo avec `queryType: serviceMap`.
3. **Codes HTTP 201 dans la table d'erreurs** → causé par le filtre `status = error` (statut OTel du span) qui inclut des cas où une exception survient *après* la réponse. Fix: filtrer sur `span.http.response.status_code >= 400`.
4. **Noms d'attributs OTel** → l'instrumentation .NET utilise les conventions récentes: `http.request.method` (pas `http.method`), `http.response.status_code` (pas `http.status_code`), `error.type` au lieu d'`exception.*`.

## Limites connues / améliorations possibles

- **Angular non instrumenté** → apparaît comme nœud `user` (virtual) dans le service graph. Pour le vrai bout-en-bout, instrumenter avec `@opentelemetry/sdk-trace-web` + propagation `traceparent`.
- **MySQL invisible** dans le service graph → ajouter `peer_attributes: [db.name, db.system, server.address]` dans `tempo.yaml` pour générer des virtual nodes DB.
- **Métriques `traces_spanmetrics_*` staleness** → après un restart, le metrics-generator n'a rien à pousser tant qu'aucune trace n'est arrivée. Générer du trafic pour réamorcer.

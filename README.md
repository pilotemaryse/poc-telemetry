# DockerCrudDemo

.NET 9 Clean Architecture + Angular 19 + MySQL 8 + RabbitMQ + shared observability stack.

## Architecture

```
src/
  Domain/          # Entities + domain events
  Application/     # Interfaces, DTOs, ProductService
  Infrastructure/  # EF Core, repositories, MassTransit
  Api/             # ASP.NET Core controllers
  Worker/          # Background consumer of RabbitMQ events
web/               # Angular app (nginx, /api → api:8080)
observability/     # Shared telemetry stack (Grafana + Tempo + Loki + Mimir + otel-collector)
docker-compose.yml # Project services only
```

## Run

The observability stack is **separate** so multiple projects can share one Grafana.

### One-time setup

```bash
docker network create observability
```

### Start observability stack (runs once, shared across projects)

```bash
cd observability
docker compose up -d
```

- Grafana: http://localhost:3000 (anonymous Admin)
- Mimir (metrics): http://localhost:9009
- Loki (logs): http://localhost:3100
- Tempo (traces): http://localhost:3200

### Start this project

```bash
docker compose up --build -d
```

- Frontend: http://localhost:4200
- API: http://localhost:5000/api/products
- RabbitMQ UI: http://localhost:15672 (guest/guest)

## Adding a new project to the same observability stack

In the new project's `docker-compose.yml`:

1. Reference the `observability` external network:
   ```yaml
   networks:
     default:
     observability:
       external: true
       name: observability
   ```

2. Attach services that emit telemetry to both networks:
   ```yaml
   api:
     networks: [default, observability]
     environment:
       OTEL_EXPORTER_OTLP_ENDPOINT: http://otel-collector:4317
       OTEL_RESOURCE_ATTRIBUTES: "project=my-new-project"
   ```

3. (Optional) For RabbitMQ Prometheus scraping, add the discovery label:
   ```yaml
   rabbitmq:
     labels:
       telemetry.scrape: rabbitmq
     networks: [default, observability]
   ```

The `project` label flows through automatically:
- App metrics → set via `OTEL_RESOURCE_ATTRIBUTES`
- Container metrics → otel-collector reads compose project name from Docker
- RabbitMQ → prometheus discovers via Docker labels and tags with project

In Grafana, the **Observability Overview** dashboard has a `$project` dropdown — select your new project to see its metrics in isolation.

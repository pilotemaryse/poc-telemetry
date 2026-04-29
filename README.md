# DockerCrudDemo

.NET 9 Clean Architecture + Angular 19 + MySQL 8, all in Docker.

## Structure

```
src/
  Domain/          # Entities (Product)
  Application/     # Interfaces, DTOs
  Infrastructure/  # EF Core DbContext, repositories, MySQL (Pomelo)
  Api/             # ASP.NET Core controllers
web/               # Angular app (served by nginx, proxies /api → api:8080)
docker-compose.yml
```

## Run

```bash
docker compose up --build
```

- Frontend: http://localhost:4200
- API: http://localhost:5000/api/products
- MySQL: localhost:3306 (user `appuser` / pass `apppass` / db `appdb`)

The API auto-creates the schema on startup via `EnsureCreated()`.

## Local dev (without Docker)

```bash
# API (needs MySQL running on localhost:3306)
dotnet run --project src/Api

# Web
cd web && npm install && npm start
```

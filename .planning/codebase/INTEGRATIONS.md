# Integrations

## Databases
- Primary database: PostgreSQL via container `db` in `compose.yaml`.
- Backend DB integration is configured through EF Core `UseNpgsql(...)` in `BitNest/Program.cs`.
- Connection string key: `ConnectionStrings:DefaultConnection` in `BitNest/appsettings.json` and overridden by `ConnectionStrings__DefaultConnection` in `compose.yaml`.

## Container and Runtime Integrations
- Docker Compose orchestrates `api`, `db`, and `frontend` in `compose.yaml`.
- Persistent volumes:
  - `pgdata` for PostgreSQL at `/var/lib/postgresql/data` in `compose.yaml`.
  - `bit_storage` for uploaded file/chunk storage at `/app/data/storage` in `compose.yaml`.
- Backend and frontend are connected by internal Docker DNS hostname `frontend` in reverse proxy config in `BitNest/appsettings.json`.

## Reverse Proxy Integration
- YARP route catches all paths and sends them to frontend cluster in `BitNest/appsettings.json`.
- Reverse proxy is registered in `BitNest/Program.cs` with `AddReverseProxy().LoadFromConfig(...)`.
- Request flow: browser -> API container (`5000`) -> proxy to frontend container (`frontend:80`) for non-controller routes.

## External Services and APIs
- No third-party SaaS APIs are directly called from application code.
- No external auth provider integration is present.
- No webhook producers/consumers found in the current code.

## CI/CD and Registry Integration
- GitHub Actions workflow in `.github/workflows/docker-image.yml` integrates with Docker Hub.
- Secrets expected in repo settings:
  - `DOCKERHUB_USERNAME`
  - `DOCKERHUB_TOKEN`
- Tagged releases (`v*`) trigger image push for API and frontend.

## Security-Relevant Integration Notes
- Database credentials are hardcoded in `compose.yaml` and also appear in `BitNest/appsettings.json`.
- CORS policy `AllowAll` is configured in `BitNest/Program.cs`, acceptable for local/dev but high risk in public deployments.
- Upload endpoint accepts very large bodies in `BitNest/Program.cs`; operational controls should exist at infra layer.

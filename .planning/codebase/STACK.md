# Stack

## Languages and Runtime
- Backend: C# on ASP.NET Core, target framework `net9.0` in `BitNest/BitNest.csproj`.
- Frontend: Vanilla JavaScript, HTML, and CSS in `FrontEnd/main.js`, `FrontEnd/index.html`, `FrontEnd/style.css`.
- Data store: PostgreSQL 16 in `compose.yaml`.
- Deployment runtime: Linux containers via Docker and Docker Compose in `BitNest/Dockerfile`, `FrontEnd/Dockerfile`, and `compose.yaml`.

## Backend Frameworks and Packages
- Web framework: ASP.NET Core Web API from `Microsoft.NET.Sdk.Web` in `BitNest/BitNest.csproj`.
- API and docs: `Microsoft.AspNetCore.OpenApi` in `BitNest/BitNest.csproj` and `app.MapOpenApi()` in `BitNest/Program.cs`.
- ORM: Entity Framework Core (`Microsoft.EntityFrameworkCore`) with PostgreSQL provider (`Npgsql.EntityFrameworkCore.PostgreSQL`) in `BitNest/BitNest.csproj`.
- Logging: Serilog (`Serilog`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`) configured in `BitNest/Program.cs` and `BitNest/appsettings.json`.
- Proxying: YARP (`Yarp.ReverseProxy`) configured in `BitNest/Program.cs` and `BitNest/appsettings.json`.
- Hashing and dedupe support: `Blake3` package used in `BitNest/Services/StorageService.cs`.

## Frontend Stack
- No build system detected; static assets served directly from `FrontEnd/`.
- API interactions use browser `fetch` and `XMLHttpRequest` in `FrontEnd/main.js`.
- Styling is hand-written CSS with responsive rules and custom properties in `FrontEnd/style.css`.

## Configuration and Environment
- Connection string defined in `BitNest/appsettings.json` and overridden in container env vars in `compose.yaml`.
- Upload path configured via `UploadsPath` setting in `BitNest/appsettings.json` and `compose.yaml`.
- Kestrel and form limits are configured in `BitNest/Program.cs` (unbounded request body and multipart limits).

## Build and Delivery
- Solution file: `BitNest.sln`.
- Backend container built from `BitNest/Dockerfile`.
- Frontend container built from `FrontEnd/Dockerfile` (nginx static serving).
- CI/CD workflow: `.github/workflows/docker-image.yml` builds multi-arch images on tags and validates PR builds.

## Observations
- Architecture is intentionally simple: single backend project plus static frontend.
- Dependency set is small and focused on API, persistence, logging, proxying, and chunk hashing.
- No frontend framework or package manager artifacts are present in `FrontEnd/`.

# Structure

## Repository Layout
- `BitNest.sln`: solution root linking backend project.
- `BitNest/`: ASP.NET Core backend project.
- `FrontEnd/`: static frontend project served by nginx.
- `.github/workflows/`: CI workflow definitions.
- `.planning/codebase/`: generated codebase mapping documents.

## Backend Directory Map (`BitNest/`)
- `Program.cs`: service registration, middleware setup, startup behavior.
- `Controllers/StorageController.cs`: HTTP endpoints for upload, list, download, delete.
- `Services/StorageService.cs`: chunking, dedupe, metadata operations.
- `Services/ChunkedFileStream.cs`: stream composition across persisted chunk files.
- `Data/AppDbContext.cs`: EF Core DbContext and key model configuration.
- `Models/`: persistence entities (`FileMetadata`, `FileChunk`, `ChunkMetadata`).
- `DTOs/FileMetadataDTO.cs`: list response transfer shape.
- `Extensions/ChunksExtensions.cs`: chunk path and naming helpers.
- `Migrations/`: EF Core migration history and snapshots.
- `appsettings.json` and `appsettings.Development.json`: runtime config.

## Frontend Directory Map (`FrontEnd/`)
- `index.html`: app shell and layout markup.
- `main.js`: upload/list/delete/download interactions and DOM rendering.
- `style.css`: visual theme and responsive rules.
- `Dockerfile`: nginx-based static hosting image.

## Naming and Organization Patterns
- Backend uses PascalCase for classes and methods in C# files.
- Controller route token uses class name convention (`[Route("[controller]")]`).
- DTO and model folders are separated, but there is minimal deep layering.
- Frontend files are flat and single-purpose (one JS, one CSS, one HTML).

## Key Config and Ops Files
- `compose.yaml`: local runtime wiring, env vars, ports, and volumes.
- `.github/workflows/docker-image.yml`: container build and publish automation.
- `BitNest/Dockerfile`: multi-stage .NET image build.
- `FrontEnd/Dockerfile`: static file copy into nginx.

## Growth Notes
- Backend currently centralizes logic in `StorageService`; likely split point as domain grows.
- Frontend lacks component/module partitioning, expected for current MVP scope.

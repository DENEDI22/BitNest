# Architecture

## System Shape
- Monorepo with two deployable units:
  - Backend API in `BitNest/`.
  - Static frontend in `FrontEnd/`.
- Runtime topology is defined in `compose.yaml` (`api`, `db`, `frontend`).

## Backend Architectural Pattern
- Pattern: lightweight layered MVC/service architecture.
- Entry point and composition root: `BitNest/Program.cs`.
- HTTP interface layer: `BitNest/Controllers/StorageController.cs`.
- Domain/data access logic: `BitNest/Services/StorageService.cs` and `BitNest/Data/AppDbContext.cs`.
- Persistence model layer: `BitNest/Models/*.cs`.

## Core Data Flow
- Upload path:
  - `POST /Storage` in `BitNest/Controllers/StorageController.cs`.
  - Delegates to `StorageService.UploadFile(...)` in `BitNest/Services/StorageService.cs`.
  - File stream is chunked, each chunk hashed (BLAKE3), deduplicated against `Chunks` table, and stored in filesystem.
  - Metadata and chunk relations persisted through EF Core in `BitNest/Data/AppDbContext.cs`.
- Download path:
  - `GET /Storage/download/{fileId}` in `BitNest/Controllers/StorageController.cs`.
  - Service loads ordered chunk list and returns `ChunkedFileStream` from `BitNest/Services/ChunkedFileStream.cs`.
  - Response body streams reconstructed file.
- List/delete path:
  - Pagination and projection in `StorageService.GetFilesAsJson(...)`.
  - Soft delete via `IsDeleted` flag in `StorageService.SafeDeleteFile(...)`.

## Frontend Interaction Pattern
- `FrontEnd/main.js` handles upload/list/download/delete calls to backend.
- API base URL is derived from `window.location.origin` with port remap in `FrontEnd/main.js`.
- UI renders current page entries and upload progress directly with DOM APIs.

## Infrastructure and Routing
- API container exposes port 8080 internally, mapped to host 5000 in `compose.yaml`.
- Frontend nginx exposes port 80 internally, mapped to host 3000 in `compose.yaml`.
- YARP in backend proxies catch-all frontend routes to `http://frontend:80/` from `BitNest/appsettings.json`.

## Architectural Constraints
- Single service and single controller currently carry most business behavior.
- Request body and multipart limits are set to max in `BitNest/Program.cs`.
- Database migration is auto-applied at startup in `BitNest/Program.cs`.

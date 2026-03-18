# Testing

## Current State
- No dedicated test project was found in repository root (`BitNest.sln` currently references the backend project only).
- No files matching common test patterns (`*Tests.cs`, `*.spec.js`, `*.test.js`) were found in `BitNest/` or `FrontEnd/`.
- No CI test execution step is present in `.github/workflows/docker-image.yml`.

## What Is Verified Today
- CI currently validates container image buildability for pull requests in `.github/workflows/docker-image.yml`.
- Tagged releases build and push API/frontend images, which implicitly verifies Docker packaging.
- Runtime correctness appears to be manually validated by running via `compose.yaml`.

## Missing Automated Coverage
- Backend unit tests for `StorageService` chunking and dedupe behavior.
- Backend integration tests for `StorageController` endpoints and DB interactions.
- Frontend behavioral tests for upload progress, pagination, and error states in `FrontEnd/main.js`.
- Contract tests ensuring API DTO shape compatibility with frontend assumptions.

## Suggested Test Structure
- Add `BitNest.Tests/` xUnit project for service and controller tests.
- Use `WebApplicationFactory` for API integration tests against test DB container.
- Add lightweight frontend test runner (for example Vitest + jsdom) if frontend complexity grows.
- Introduce deterministic fixtures for chunk-hash dedupe paths in `StorageService.UploadFile(...)`.

## Suggested CI Enhancements
- Run `dotnet test` before Docker build in `.github/workflows/docker-image.yml`.
- Add migration validation step (`dotnet ef migrations script` or startup smoke test).
- Add compose smoke test to validate `api` <-> `db` <-> `frontend` wiring.

## Risk Notes
- Upload/download core logic currently lacks automated regression safety net.
- Deduplication and chunk ordering logic are sensitive to subtle stream and hash bugs.
- No security tests are present for large request handling and CORS behavior.

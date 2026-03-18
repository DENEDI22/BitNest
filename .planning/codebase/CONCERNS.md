# Concerns

## High-Priority Technical Risks
- CORS allows any origin, method, and header in `BitNest/Program.cs`; this is unsafe for public production exposure.
- Request size and multipart limits are effectively unbounded in `BitNest/Program.cs`, creating potential resource exhaustion vectors.
- Plaintext DB credentials are committed in `compose.yaml` and also visible in `BitNest/appsettings.json`.
- Reverse proxy catch-all route in `BitNest/appsettings.json` may mask routing mistakes and makes path governance harder.

## Data Integrity and Correctness Risks
- Chunk hash generation in `BitNest/Services/StorageService.cs` hashes full buffer (`buffer`) each iteration, not just bytes read (`i`), which can cause incorrect dedupe for last chunk.
- `StorageService.UploadFile(...)` returns `fileName + extension` while `fileName` already appears to include extension from controller call, causing duplicated suffixes.
- Download endpoint catches broad exceptions and suppresses diagnostics in `BitNest/Controllers/StorageController.cs`.
- `ChunkedFileStream` has unimplemented `Length`/`Position` semantics in `BitNest/Services/ChunkedFileStream.cs`, which can affect compatibility for advanced stream consumers.

## Maintainability Concerns
- `StorageService` is taking multiple responsibilities (metadata persistence, chunking, dedupe, filesystem IO, serialization).
- Frontend logic is single-file and global-state-driven in `FrontEnd/main.js`, likely to become hard to extend.
- Mixed naming quality (for example `Extention`) indicates lack of enforced linting/analyzers.
- No explicit abstraction around filesystem storage or hashing strategy.

## Testing and Release Concerns
- No automated test suite detected, despite complex chunking and stream reconstruction behavior.
- CI validates image builds but not functional behavior.
- Auto-running `db.Database.Migrate()` at startup in `BitNest/Program.cs` can cause operational surprises in shared environments.

## Security and Operational Follow-Ups
- Move secrets to environment/secret store and scrub defaults from tracked configs.
- Add request throttling, upload size guardrails, and auth before exposing externally.
- Implement structured error responses with traceable IDs for easier incident triage.
- Add observability around upload failures, chunk dedupe rates, and storage growth.

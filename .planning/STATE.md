---
gsd_state_version: 1.0
milestone: v0.0.3
milestone_name: milestone
status: Ready to execute
stopped_at: Completed 10-01-PLAN.md
last_updated: "2026-03-26T10:30:51.237Z"
progress:
  total_phases: 8
  completed_phases: 4
  total_plans: 14
  completed_plans: 11
---

# Project State

## Project Reference

See: `.planning/PROJECT.md` (updated 2026-03-26)

**Core value:** Users can reliably store and retrieve files on their own infrastructure with a simple web workflow.
**Current focus:** Phase 10 — linux-x86-64-installer

## Current Position

Phase: 10 (linux-x86-64-installer) — EXECUTING
Plan: 2 of 4

## Accumulated Context

### Decisions

- Bootstrapped GSD planning artifacts from existing BitNest codebase and codebase map.
- [Phase 06]: Use PBKDF2-SHA256 with versioned hash payload v1.iterations.salt.hash for password storage compatibility.
- [Phase 06]: Enforce username uniqueness with a database-level unique index on NormalizedUsername.
- [Phase 06]: Keep auth tests runtime-safe by linking auth source files while preserving BitNest project reference metadata.
- [Phase 06]: Use JWT bearer access tokens (15 min) with hashed opaque refresh secrets persisted in RefreshSessions.
- [Phase 06]: Standardize auth failures as { code, message } via shared AuthErrorDto and controller result mapping.
- [Phase 06]: Run endpoint contract tests with a TestServer host and in-memory EF database to validate refresh rotation behavior.
- [Phase 06]: Keep frontend auth orchestration deterministic via JS-source-level tests for startup ordering and session transitions.
- [Phase 06]: Persist refresh tokens in sessionStorage or localStorage based on remember-me while keeping access tokens memory-only.
- [Phase 06]: Gate protected file actions through auth checks so stale sessions fall back to sign-in with inline messaging.
- [Phase 07]: Modeled grants as a dedicated FileGrant entity with unique index on (FileId, GrantedUserId) to prevent duplicate grants.
- [Phase 07]: Configured grant user foreign keys with Restrict delete behavior to avoid accidental user-chain deletions.
- [Phase 08]: Use RandomNumberGenerator.GetBytes(64) for sharepoint tokens with SHA256 hashed storage (never persist raw tokens)
- [Phase 08]: Configure SharepointLink foreign keys with Cascade delete on File (file deleted → links deleted) and Restrict on CreatedBy user
- [Phase 08]: Created standalone public download page (share.html) separate from main SPA for unauthenticated access
- [Phase 08]: Use navigator.clipboard API with instant visual feedback for copy-to-clipboard throughout sharepoint UI
- [Phase 09]: Use UploadSlotValidationResult discriminated union (IsValid/IsSlotFull) for validation results at API boundary instead of exceptions
- [Phase 09]: InMemory provider catch fallback in ValidateAndReserveUploadSlotAsync allows tests to use same code path; production uses Postgres ExecuteUpdateAsync
- [Phase 09]: ValidateTokenAndGetLinkAsync is the unified link resolver; callers branch on LinkType for download vs upload behavior
- [Phase 09]: CreateLinkAsync baseUrl parameter made optional (default empty string) to maintain backward compat with existing tests
- [Phase 09]: Public upload page carries no Authorization header — URL token is the credential, preventing auth leakage
- [Phase 09]: Slot-full transition handled at page load and on 409 response mid-upload for consistent UX
- [v0.1.0]: Three separate Python installer scripts (one per platform: linux-x86_64, linux-arm64, windows-wsl2)
- [v0.1.0]: Python stdlib only — no pip install required from end-users
- [v0.1.0]: Pull from Docker Hub pre-built images (not local build)
- [v0.1.0]: Bind-mount volumes to user-defined directory with data/storage and data/postgres subdirs
- [v0.1.0]: One self-contained Python file per platform — no shared module, no installer dependencies
- [v0.1.0]: compose.yaml embedded as Python string constant using str.format() — never string.Template (conflicts with Docker ${VAR} syntax)
- [v0.1.0]: State file at ~/.config/bitnest/install.json (XDG Base Dir); never store secrets in state file
- [v0.1.0]: docker compose (space, V2 plugin) exclusively; docker-compose (hyphen, V1) is EOL and unsupported
- [v0.1.0]: secrets.token_hex(32) for all secret generation — hex-only output prevents .env interpolation breakage
- [v0.1.0]: Port conflict pre-flight via socket.bind() check before first wizard prompt (not after wizard completes)
- [v0.1.0]: pg_isready healthcheck with condition: service_healthy embedded in compose template from Phase 10 — cannot be retrofitted
- [v0.1.0]: Phase 10 freezes all shared patterns (argparse structure, compose template, state schema, subprocess wrapper) before ARM64/WSL2 variants are written
- [v0.1.0]: Phase 11 (ARM64) depends on Phase 10 being frozen; Phase 12 (WSL2) depends on Phase 10 but can run parallel to Phase 11
- [Phase 10]: Changed Program.Main to async Task Main to support await in startup admin seeding block
- [Phase 10]: Admin seeding skipped silently when BITNEST_ADMIN_USER or BITNEST_ADMIN_PASS is absent — no noise for non-installer deployments

### Pending Todos

- Confirm Docker Hub username (`DOCKERHUB_USERNAME`) before Phase 10 implementation — hardcoded into installer image references.
- Verify current GitHub Actions pipeline publishes `linux/arm64` manifests before Phase 11 planning — blocker for ARM64 acceptance testing.

### Blockers/Concerns

- Docker Hub username (`DOCKERHUB_USERNAME`) is a CI/CD secret and must be confirmed before implementation to hardcode in installer scripts.
- Specialized `gsd-*` subagents currently fail to spawn in this runtime (`ProviderModelNotFoundError`), so workflows may require in-context fallback.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260324-ccs | Fix expiry date field not populating when quick-select buttons clicked | 2026-03-24 | 6047ed0 | [260324-ccs-fix-expiry-date-field-not-populating-whe](./quick/260324-ccs-fix-expiry-date-field-not-populating-whe/) |

## Performance Metrics

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 06 P01 | 355s | 3 tasks | 9 files |
| Phase 06 P02 | 567s | 3 tasks | 15 files |
| Phase 06 P03 | 2540s | 3 tasks | 4 files |
| Phase 07 P01 | 1320 | 2 tasks | 9 files |
| Phase 08-sharepoint-expiring-download-links P02 | 8 min | 4 tasks | 6 files |
| Phase 09 P01 | 5 min | 2 tasks | 13 files |
| Phase 09-sharepoint-dropbox-upload P02 | 20min | 3 tasks | 4 files |
| Phase 10 P01 | 250 | 2 tasks | 12 files |

## Session Continuity

Last session: 2026-03-26T10:30:51.232Z
Stopped at: Completed 10-01-PLAN.md
Resume file: None

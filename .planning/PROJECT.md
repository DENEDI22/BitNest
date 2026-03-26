# BitNest

## What This Is

BitNest is a self-hosted cloud storage app for personal or small-team use. It provides file upload, listing, download, and delete via an ASP.NET Core API with PostgreSQL metadata and chunk-based on-disk storage. Users authenticate with JWT-based sessions, access only their own files, and can share files via expiring sharepoint links (download or dropbox-style upload). A one-command Linux installer with Textual TUI handles deployment.

## Core Value

Users can reliably store and retrieve files on their own infrastructure with a simple web workflow.

## Current State

**Shipped:** v0.0.3 (2026-03-26)

All auth, access control, sharepoint, and installer features delivered. Ready for next milestone planning.

## Requirements

### Validated

- ✓ User can upload files through the web UI and API — v1.0
- ✓ User can browse paginated file metadata — v1.0
- ✓ User can download stored files — v1.0
- ✓ User can soft-delete files — v1.0
- ✓ System stores metadata in PostgreSQL and file chunks on disk — v1.0
- ✓ Users can sign up, sign in, sign out, and manage account basics — v0.0.3
- ✓ Admin can view and manage user accounts — v0.0.3
- ✓ Authenticated users can access only own/granted file metadata — v0.0.3
- ✓ Users can generate expiring sharepoint links for selected files — v0.0.3
- ✓ Third-party users can use sharepoint links to download and upload (dropbox-style) — v0.0.3
- ✓ Linux x86_64 installer with Textual TUI for one-command self-hosted deployment — v0.0.3

### Active

*(None — awaiting next milestone definition)*

### Out of Scope

- OAuth and external identity providers — not required for current self-hosted MVP
- Multi-tenant workspace management — current product targets single deployment owner

## Context

- Backend: ASP.NET Core (`net9.0`) with EF Core and Npgsql.
- Frontend: static HTML/CSS/JS app.
- Deployment: Docker Compose (`api`, `db`, `frontend`) + GitHub Actions image builds + Linux x86_64 Textual TUI installer.
- Codebase map created in `.planning/codebase/` as baseline architecture reference.

## Constraints

- **Tech stack**: Keep current .NET + PostgreSQL + static frontend approach — minimize rewrite risk.
- **Deployment**: Must remain self-hostable via Docker Compose — supports Raspberry Pi/home-lab usage.
- **Data durability**: File and DB persistence must survive container restarts — protects user data.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Chunk-based file storage with dedupe metadata | Reduce duplicate storage and enable large file handling | ✓ Good |
| Reverse proxy frontend through API service | Keep a single access pattern while preserving separate frontend container | ✓ Good |
| Bootstrap planning docs from existing brownfield codebase | Enable milestone-driven workflow on pre-existing project | ✓ Good |
| PBKDF2-SHA256 with versioned hash payload for passwords | Forward-compatible hashing with iteration count embedded in stored value | ✓ Good |
| JWT bearer (15 min) + hashed opaque refresh secrets in DB | Stateless access tokens + revocable refresh sessions | ✓ Good |
| FileGrant entity with unique index (FileId, GrantedUserId) | Prevent duplicate grants; Restrict FK delete avoids accidental chain deletion | ✓ Good |
| Sharepoint tokens stored as SHA256 hash only | Raw token never persisted — token is credential, hash is storage | ✓ Good |
| Upload slot validation via discriminated union (not exceptions) | Clean API boundary for IsValid/IsSlotFull states | ✓ Good |
| Single Python installer script with Textual TUI | Zero external dependencies beyond Python stdlib + textual; portable | ✓ Good |
| Installer requires `sudo` at startup | Avoids partial-failure mid-install due to missing permissions | ✓ Good |

---
*Last updated: 2026-03-26 after v0.0.3 milestone complete — all auth, sharepoint, and installer phases shipped*

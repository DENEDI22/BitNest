# BitNest

## What This Is

BitNest is a self-hosted cloud storage app for personal or small-team use. It provides file upload, listing, download, and delete via an ASP.NET Core API with PostgreSQL metadata and chunk-based on-disk storage. A lightweight web UI allows users to manage files from the browser.

## Core Value

Users can reliably store and retrieve files on their own infrastructure with a simple web workflow.

## Requirements

### Validated

- ✓ User can upload files through the web UI and API — v1.0
- ✓ User can browse paginated file metadata — v1.0
- ✓ User can download stored files — v1.0
- ✓ User can soft-delete files — v1.0
- ✓ System stores metadata in PostgreSQL and file chunks on disk — v1.0

### Active

- [ ] Define next milestone scope

### Out of Scope

- OAuth and external identity providers — not required for current self-hosted MVP
- Multi-tenant workspace management — current product targets single deployment owner

## Context

- Backend: ASP.NET Core (`net9.0`) with EF Core and Npgsql.
- Frontend: static HTML/CSS/JS app.
- Deployment: Docker Compose (`api`, `db`, `frontend`) and GitHub Actions image builds.
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

---
*Last updated: 2026-03-19 after brownfield planning bootstrap*

# BitNest

## What This Is

BitNest is a self-hosted cloud storage app for personal or small-team use. It provides file upload, listing, download, and delete via an ASP.NET Core API with PostgreSQL metadata and chunk-based on-disk storage. A lightweight web UI allows users to manage files from the browser.

## Core Value

Users can reliably store and retrieve files on their own infrastructure with a simple web workflow.

## Current Milestone: v0.1.0 Distribution & Installer

**Goal:** Ship guided Python installers so end-users can deploy BitNest on their own machines without knowledge of Docker Compose.

**Target features:**
- Linux x86_64 installer with Docker auto-install, config wizard, and compose orchestration
- Linux ARM64 installer (Raspberry Pi support)
- Windows WSL2 installer with Docker Desktop guidance

## Requirements

### Validated

- ✓ User can upload files through the web UI and API — v1.0
- ✓ User can browse paginated file metadata — v1.0
- ✓ User can download stored files — v1.0
- ✓ User can soft-delete files — v1.0
- ✓ System stores metadata in PostgreSQL and file chunks on disk — v1.0
- ✓ Users can sign up, sign in, sign out, and manage account basics — v0.0.3-alpha Phase 6
- ✓ Admin can view and manage user accounts — v0.0.3-alpha Phase 7
- ✓ Authenticated users can access only own/granted file metadata — v0.0.3-alpha Phase 7
- ✓ Users can generate expiring sharepoint links for selected files — v0.0.3-alpha Phase 8
- ✓ Third-party users can use sharepoint links to download and upload (dropbox-style) — v0.0.3-alpha Phase 9

### Active

- [ ] User can install BitNest on Linux x86_64 via a guided Python installer
- [ ] User can install BitNest on Linux ARM64 (Raspberry Pi) via a guided Python installer
- [ ] User can install BitNest on Windows via WSL2 guided Python installer
- [ ] Installer configures all required env vars and volume paths interactively
- [ ] Installer installs Docker automatically where possible
- [ ] User can update BitNest to latest images via installer
- [ ] User can uninstall BitNest (with optional data removal) via installer

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
| Milestone `v0.0.3-alpha Auth + Sharepoint` | Introduce auth + access control + temporary external link flows before broader collaboration features | ✓ Good |
| Python installer scripts (stdlib only, no pip) | End-users should not need to install dependencies to run an installer | — Pending |
| Pull from Docker Hub images (not local build) | Installers target end-users, not developers — pre-built images are appropriate | — Pending |
| Three separate installers per platform | Linux x86_64, ARM64, and Windows WSL2 have different Docker install mechanics | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-03-26 — Milestone v0.1.0 Distribution & Installer started*

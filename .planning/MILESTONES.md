# Project Milestones: BitNest

## v0.0.3 Auth + Sharepoint + Installer (Shipped: 2026-03-26)

**Phases completed:** 5 phases (6–10), 14 plans, 30+ tasks
**Timeline:** 2026-03-19 → 2026-03-26 (7 days)
**Stats:** 138 files changed, +22,686 / -511 lines, 99 commits

**Key accomplishments:**

- JWT auth system with PBKDF2 password hashing, refresh token rotation, and bearer-protected API endpoints
- Auth-first frontend with signup/login/logout and startup session bootstrap
- Admin user management panel (list, disable, create) and owner/grant-based file access enforcement
- Secure sharepoint download links with SHA256 hashed tokens, expiry validation, and public download page
- Dropbox-style upload slots via sharepoint links with atomic capacity enforcement and public upload page
- Linux x86_64 installer: single Python script with Textual TUI, Docker Compose orchestration, and 67-test unit suite

---

## v1.0 MVP (Shipped: 2026-03-19)

**Delivered:** Self-hosted file storage MVP with chunked uploads, file listing, download, and soft-delete.

**Phases completed:** 1-5 (historical, pre-GSD)

**Key accomplishments:**

- Implemented ASP.NET Core storage API and PostgreSQL metadata model
- Added chunked file storage and deduplication primitives
- Built browser UI for upload, listing, download, and delete
- Added Docker Compose runtime and CI image build workflow

**Stats:**

- Historical baseline from existing codebase (pre-planning bootstrap)
- Milestone reconstructed from current repository state

**Git range:** historical (not reconstructed)

**What's next:** v1.1 reliability, security hardening, and milestone-driven execution

---

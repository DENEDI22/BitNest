# Requirements: BitNest

**Defined:** 2026-03-19
**Milestone:** v0.1.0 Distribution & Installer
**Core Value:** Users can reliably store and retrieve files on their own infrastructure with a simple web workflow.

## v0.0.3-alpha Requirements (Complete)

All requirements from milestone `v0.0.3-alpha` have been delivered. See traceability table below.

### Authentication

- [x] **AUTH-01**: User can sign up with username and password via web frontend and API
- [x] **AUTH-02**: User can sign in with username and password via web frontend and API
- [x] **AUTH-03**: User can sign out from the web frontend and invalidate active session
- [x] **AUTH-04**: User can obtain renewed access via rotating refresh token flow

### User Management

- [x] **USER-01**: Admin can open a web frontend user-management area and list all users with account status
- [x] **USER-02**: Admin can disable a user account from the web frontend to block further access
- [x] **USER-03**: Admin can create a user account with initial username and password for handoff to a physical user

### Access Control

- [x] **ACCS-01**: Authenticated user can view metadata only for owned files or files with explicit grant access
- [x] **ACCS-02**: Download endpoint enforces owner/grant authorization before returning file content
- [x] **ACCS-03**: Delete endpoint enforces owner/grant authorization before file deletion action
- [x] **ACCS-04**: Web frontend file list and actions only surface files/actions the current user is authorized to access
- [x] **ACCS-05**: System stores grant-based access model for file permissions

### Sharepoint Links

- [x] **SHRP-01**: Authenticated user can generate a temporary sharepoint link for selected files with user-defined expiration from the web frontend
- [x] **SHRP-02**: Unauthenticated user can download only files in valid non-expired sharepoint link scope
- [x] **SHRP-03**: Unauthenticated user can upload file(s) through valid sharepoint link into scoped dropbox flow
- [x] **SHRP-04**: System rejects expired sharepoint links for both download and upload operations
- [x] **SHRP-05**: Web frontend provides sharepoint management entry points (create and view active links) for authenticated users

---

## v0.1.0 Requirements

Requirements for milestone `v0.1.0 Distribution & Installer`. Each maps to roadmap phases.

### Installer Core

- [ ] **INST-01**: User launches a Textual TUI that walks through installation step-by-step with Back/Next navigation
- [ ] **INST-02**: TUI prompts for install directory, API port, and frontend port with inline validation
- [ ] **INST-03**: Installer auto-generates a cryptographically secure DB password and JWT signing key (`secrets.token_hex`)
- [ ] **INST-04**: Installer runs prerequisite checks (Docker, Compose V2, port availability, disk space) before wizard begins
- [ ] **INST-05**: Installer creates install directory with `data/storage/` and `data/postgres/` subdirectories
- [ ] **INST-06**: Installer writes `.env` (chmod 600) and `compose.yaml` with bind mounts and `pg_isready` healthcheck
- [ ] **INST-07**: Installer shows a live progress screen with output while pulling Docker Hub images
- [ ] **INST-08**: Installer starts the stack with DB-before-API ordering via `condition: service_healthy`
- [ ] **INST-09**: Installer polls per-service health and shows pass/fail status before declaring success
- [ ] **INST-10**: Installer saves install state to `~/.config/bitnest/install.json`
- [ ] **INST-11**: User can launch the update flow via TUI to pull latest images and rolling-restart the stack
- [ ] **INST-12**: User can launch the uninstall flow via TUI with explicit confirmation before any data is deleted

### Linux Docker Auto-Install

- [ ] **LINUX-01**: Installer detects missing Docker Engine and installs it automatically (apt/dnf/pacman with get.docker.com fallback)
- [ ] **LINUX-02**: Installer escalates to sudo only for Docker install steps; all other operations run as current user

### ARM64 / Raspberry Pi

- [ ] **ARM-01**: ARM64 installer configures Docker apt repository with `arch=arm64`
- [ ] **ARM-02**: ARM64 installer verifies `linux/arm64` manifest exists on Docker Hub before pulling
- [ ] **ARM-03**: ARM64 installer displays a non-blocking low-RAM advisory when system memory is below 2 GB

### Windows WSL2

- [ ] **WSL-01**: Installer detects WSL2 environment via `/proc/sys/kernel/osrelease`
- [ ] **WSL-02**: Installer checks Docker Desktop is reachable; guides user to install/start it if not
- [ ] **WSL-03**: Installer warns and re-prompts when user selects a `/mnt/` path for data (performance + PostgreSQL permission issues)

### Distribution & CI

- [ ] **DIST-01**: Each installer has a `pyproject.toml` and PyInstaller spec file to produce a standalone binary
- [ ] **DIST-02**: GitHub Actions builds PyInstaller binaries for all three platforms on git tag push (linux-x86_64 on `ubuntu-latest`, linux-arm64 on `ubuntu-24.04-arm`, windows-wsl2 on `ubuntu-latest`), creates a GitHub Release, and attaches all three binaries as release assets

### Automation Flags

- [ ] **AUTO-01**: All installers support `--yes` / `--non-interactive` flag to accept all defaults without prompts
- [ ] **AUTO-02**: All installers accept `--install-dir`, `--api-port`, `--frontend-port` CLI flags to pre-fill wizard values

## v2 Requirements

Deferred to future release.

### Collaboration

- **COLL-01**: Authenticated users can directly share files with other registered users
- **COLL-02**: Authenticated users can manage incoming/outgoing cross-user share permissions

### Installer Enhancements

- **IUPG-01**: Installer supports `--dry-run` mode to preview all changes without executing them
- **IUPG-02**: Installer backs up `.env` and `compose.yaml` before running an update
- **IUPG-03**: Installer checks installed image tag against Docker Hub latest and reports whether an update is available
- **IUPG-04**: Installer generates a `systemd` service unit for auto-start on boot

## Out of Scope

| Feature | Reason |
|---------|--------|
| Cross-user file sharing UX | Deferred by milestone scope to avoid permission-model expansion |
| OAuth/social login | Username/password sufficient for self-hosted MVP |
| Mobile-native clients | Current milestone targets existing web frontend only |
| TLS/HTTPS setup (Caddy/Certbot) | Significant complexity; out of scope for v0.1.0 |
| arm32 / armv7l support | Docker image variants not confirmed; edge case deferred |
| Installer auto-update on run | Privacy violation for a data-sovereignty product |
| Telemetry / phone-home | Violates core self-hosted privacy value |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTH-01 | Phase 6 | Complete |
| AUTH-02 | Phase 6 | Complete |
| AUTH-03 | Phase 6 | Complete |
| AUTH-04 | Phase 6 | Complete |
| USER-01 | Phase 7 | Complete |
| USER-02 | Phase 7 | Complete |
| USER-03 | Phase 7 | Complete |
| ACCS-01 | Phase 7 | Complete |
| ACCS-02 | Phase 7 | Complete |
| ACCS-03 | Phase 7 | Complete |
| ACCS-04 | Phase 7 | Complete |
| ACCS-05 | Phase 7 | Complete |
| SHRP-01 | Phase 8 | Complete |
| SHRP-02 | Phase 8 | Complete |
| SHRP-03 | Phase 9 | Complete |
| SHRP-04 | Phase 8 | Complete |
| SHRP-05 | Phase 8 | Complete |
| INST-01 | Phase 10 | Pending |
| INST-02 | Phase 10 | Pending |
| INST-03 | Phase 10 | Pending |
| INST-04 | Phase 10 | Pending |
| INST-05 | Phase 10 | Pending |
| INST-06 | Phase 10 | Pending |
| INST-07 | Phase 10 | Pending |
| INST-08 | Phase 10 | Pending |
| INST-09 | Phase 10 | Pending |
| INST-10 | Phase 10 | Pending |
| INST-11 | Phase 10 | Pending |
| INST-12 | Phase 10 | Pending |
| LINUX-01 | Phase 10 | Pending |
| LINUX-02 | Phase 10 | Pending |
| ARM-01 | Phase 11 | Pending |
| ARM-02 | Phase 11 | Pending |
| ARM-03 | Phase 11 | Pending |
| WSL-01 | Phase 12 | Pending |
| WSL-02 | Phase 12 | Pending |
| WSL-03 | Phase 12 | Pending |
| DIST-01 | Phase 13 | Pending |
| DIST-02 | Phase 13 | Pending |
| AUTO-01 | Phase 13 | Pending |
| AUTO-02 | Phase 13 | Pending |

**Coverage:**
- v0.1.0 requirements: 24 total
- Mapped to phases: 24/24
- Unmapped: 0

| Phase | Requirements | Count |
|-------|-------------|-------|
| Phase 10 | INST-01 through INST-12, LINUX-01, LINUX-02 | 14 |
| Phase 11 | ARM-01, ARM-02, ARM-03 | 3 |
| Phase 12 | WSL-01, WSL-02, WSL-03 | 3 |
| Phase 13 | DIST-01, DIST-02, AUTO-01, AUTO-02 | 4 |

---
*Requirements defined: 2026-03-19*
*Last updated: 2026-03-26 — v0.1.0 roadmap created; all 24 requirements mapped to phases 10-13*

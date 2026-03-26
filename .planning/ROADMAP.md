# Roadmap: BitNest

## Overview

This roadmap continues from historical phases 1-5 and defines milestone `v0.0.3-alpha Auth + Sharepoint` as phases 6-9. It introduces authentication and user/admin management first, then enforces file access boundaries, then adds temporary sharepoint links, and finally adds scoped public dropbox upload.

Milestone `v0.1.0 Distribution & Installer` continues as phases 10-13. It delivers three standalone Python installer scripts (Linux x86_64, Linux ARM64, Windows WSL2), a shared release distribution pipeline via PyInstaller and GitHub Actions, and automation flags across all three installers.

## Phases

- [x] **Phase 6: Identity and Session Foundation** - Add auth model, JWT lifecycle, and frontend auth entry points (completed 2026-03-19)
- [ ] **Phase 7: User Management and File Access Enforcement** - Add admin user controls and enforce owner/grant access across file flows
- [ ] **Phase 8: Sharepoint Expiring Download Links** - Add temporary scoped link generation/management and public download access
- [x] **Phase 9: Sharepoint Dropbox Upload** - Add public upload flow scoped by active sharepoint links (completed 2026-03-20)
- [ ] **Phase 10: Linux x86_64 Installer** - Deliver fully functional Linux x86_64 installer with Docker auto-install, config wizard, and compose orchestration
- [ ] **Phase 11: Linux ARM64 Installer** - Derive ARM64/Raspberry Pi installer from Phase 10 baseline with arch-specific Docker configuration
- [ ] **Phase 12: Windows WSL2 Installer** - Deliver WSL2 installer with Docker Desktop readiness checks and path validation
- [ ] **Phase 13: Distribution, CI, and Automation Flags** - Package all three installers as PyInstaller binaries, wire GitHub Actions release pipeline, and add non-interactive flags

## Phase Details

### Phase 6: Identity and Session Foundation
**Goal**: Establish secure user identity, authentication APIs, and web frontend auth flows.
**Depends on**: Phase 5
**Requirements**: [AUTH-01, AUTH-02, AUTH-03, AUTH-04]
**Success Criteria** (what must be TRUE):
  1. User can sign up and sign in from the web frontend using username and password.
  2. Authenticated API requests succeed with valid access token and fail with invalid/expired token.
  3. Refresh flow rotates refresh tokens and issues new access tokens.
  4. User can sign out and prior session credentials are no longer accepted.
**Plans**: 3 plans

Plans:
- [ ] 06-01-PLAN.md — Add auth entities, password hashing primitives, and migration baseline
- [ ] 06-02-PLAN.md — Implement `/auth/*` APIs, refresh rotation, and JWT middleware wiring
- [ ] 06-03-PLAN.md — Build auth-first frontend flow with signup/login/logout and startup auth gate

### Phase 7: User Management and File Access Enforcement
**Goal**: Provide admin user controls and enforce owner/grant-based access in existing storage flows.
**Depends on**: Phase 6
**Requirements**: [USER-01, USER-02, USER-03, ACCS-01, ACCS-02, ACCS-03, ACCS-04, ACCS-05]
**Success Criteria** (what must be TRUE):
  1. Admin can list users, disable users, and create new user accounts from web frontend controls.
  2. File metadata list returns only files owned by current user or explicitly granted.
  3. Download and delete operations reject unauthorized users and allow authorized users.
  4. Frontend file list/actions only render authorized resources/actions for current user.
**Plans**: 3 plans

Plans:
- [ ] 07-01-PLAN.md — Add role/admin and file-grant persistence model with migrations
- [ ] 07-02-PLAN.md — Enforce backend authorization across list/download/delete endpoints
- [ ] 07-03-PLAN.md — Add frontend admin user-management and access-aware file UI behavior

### Phase 8: Sharepoint Expiring Download Links
**Goal**: Add secure temporary sharepoint links for selected files with public download access.
**Depends on**: Phase 7
**Requirements**: [SHRP-01, SHRP-02, SHRP-04, SHRP-05]
**Success Criteria** (what must be TRUE):
  1. Authenticated user can create sharepoint links for selected files with explicit expiration.
  2. Authenticated user can view active sharepoint links in web frontend management area.
  3. Public download works only for files in active non-expired link scope.
  4. Expired links are rejected for all sharepoint access attempts.
**Plans**: 2 plans

Plans:
- [ ] 08-01: Implement sharepoint link model, token generation, expiry validation, and download API
- [ ] 08-02: Add frontend sharepoint creation and active-link management UI

### Phase 9: Sharepoint Dropbox Upload
**Goal**: Add scoped third-party upload flow using valid sharepoint links.
**Depends on**: Phase 8
**Requirements**: [SHRP-03]
**Success Criteria** (what must be TRUE):
  1. Public user can upload file(s) only through valid non-expired sharepoint link scope.
  2. Upload attempts outside scope or after expiry are rejected.
  3. Uploaded files are attributed and stored in expected owner/sharepoint context.
**Plans**: 2 plans

Plans:
- [ ] 09-01-PLAN.md — Extend SharepointLink model with upload slot support, add public upload endpoint with atomic capacity enforcement, and backend tests
- [ ] 09-02-PLAN.md — Build public upload.html page and extend links page with upload slot creation and type badges

### Phase 10: Linux x86_64 Installer
**Goal**: Users on Linux x86_64 can go from a bare machine to a running BitNest stack using a single Python script.
**Depends on**: Phase 9
**Requirements**: INST-01, INST-02, INST-03, INST-04, INST-05, INST-06, INST-07, INST-08, INST-09, INST-10, INST-11, INST-12, LINUX-01, LINUX-02
**Success Criteria** (what must be TRUE):
  1. User runs the script on a fresh Debian/Ubuntu/Fedora/RHEL/Arch machine and is guided through installation via a step-by-step TUI with Back/Next navigation.
  2. When Docker is absent, the installer detects the distro, installs Docker Engine automatically using the appropriate package manager, and escalates to sudo only for that step.
  3. User provides install directory, API port, and frontend port through prompts with inline validation; the installer generates a secure DB password and JWT key without user input.
  4. After confirming wizard values, a live progress screen shows image pull output and then per-service pass/fail health status before declaring success.
  5. User can re-run the script to reach the update flow (pulls latest images, rolling restart) or the uninstall flow (requires explicit confirmation before deleting data).
**Plans**: TBD

### Phase 11: Linux ARM64 Installer
**Goal**: Users on Linux ARM64 (Raspberry Pi and equivalent SBCs) can install BitNest using a variant installer derived from the x86_64 baseline.
**Depends on**: Phase 10
**Requirements**: ARM-01, ARM-02, ARM-03
**Success Criteria** (what must be TRUE):
  1. Installer configures the Docker apt repository with `arch=arm64` and completes Docker installation on ARM64 hardware without manual intervention.
  2. Before pulling images, the installer verifies that `linux/arm64` manifests exist on Docker Hub and aborts with a clear error message if they do not.
  3. On a system with less than 2 GB of RAM, the installer displays a non-blocking advisory about low memory before continuing installation.
**Plans**: TBD

### Phase 12: Windows WSL2 Installer
**Goal**: Users on Windows with WSL2 can install BitNest through a guided installer that handles the Docker Desktop dependency and WSL2 path constraints.
**Depends on**: Phase 10
**Requirements**: WSL-01, WSL-02, WSL-03
**Success Criteria** (what must be TRUE):
  1. Running the script inside a WSL2 distribution causes the installer to detect the WSL2 environment via `/proc/sys/kernel/osrelease` and activate WSL2-specific behavior.
  2. When Docker Desktop is not reachable from WSL2, the installer shows actionable guidance to install or start Docker Desktop rather than attempting to install Docker Engine.
  3. When the user selects a path under `/mnt/` as the install directory, the installer warns about performance and PostgreSQL permission issues and re-prompts for an alternative path.
**Plans**: TBD

### Phase 13: Distribution, CI, and Automation Flags
**Goal**: All three installers ship as standalone binaries via a GitHub Actions release pipeline, and all three support non-interactive execution for automation use cases.
**Depends on**: Phase 11, Phase 12
**Requirements**: DIST-01, DIST-02, AUTO-01, AUTO-02
**Success Criteria** (what must be TRUE):
  1. Each installer directory contains a `pyproject.toml` and a PyInstaller spec file sufficient to produce a self-contained binary with no Python runtime dependency.
  2. Pushing a git tag triggers a GitHub Actions workflow that builds all three binaries on the correct runners, creates a GitHub Release, and attaches all three binaries as release assets.
  3. Running any installer with `--yes` or `--non-interactive` completes the install flow using defaults for all prompts without requiring keyboard input.
  4. Running any installer with `--install-dir`, `--api-port`, or `--frontend-port` pre-fills those wizard values, skipping their prompts.
**Plans**: TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 6. Identity and Session Foundation | 3/3 | Complete   | 2026-03-19 |
| 7. User Management and File Access Enforcement | 1/3 | In Progress|  |
| 8. Sharepoint Expiring Download Links | 0/2 | Not started | - |
| 9. Sharepoint Dropbox Upload | 2/2 | Complete   | 2026-03-20 |
| 10. Linux x86_64 Installer | 0/? | Not started | - |
| 11. Linux ARM64 Installer | 0/? | Not started | - |
| 12. Windows WSL2 Installer | 0/? | Not started | - |
| 13. Distribution, CI, and Automation Flags | 0/? | Not started | - |

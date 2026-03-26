---
phase: 10-linux-x86-64-installer
plan: "01"
subsystem: installer-core
tags: [installer, python, docker-compose, admin-seeding, tdd]
dependency_graph:
  requires: []
  provides:
    - admin-seed-on-startup
    - installer-core-logic
    - installer-test-suite
  affects:
    - BitNest/Program.cs
    - installers/linux-x86_64/install.py
tech_stack:
  added:
    - python stdlib (secrets, socket, shutil, subprocess, pathlib, json, os)
    - pytest 8.4.2 (test runner)
  patterns:
    - str.format() compose template (not string.Template — avoids Docker ${VAR} conflict)
    - XDG config dir state file at ~/.config/bitnest/install.json
    - chmod 600 immediately after .env write
    - socket.bind() port pre-flight check
    - secrets.token_hex(32) for 64-char hex secrets
key_files:
  created:
    - installers/linux-x86_64/install.py
    - installers/linux-x86_64/tests/__init__.py
    - installers/linux-x86_64/tests/conftest.py
    - installers/linux-x86_64/tests/test_validation.py
    - installers/linux-x86_64/tests/test_preflight.py
    - installers/linux-x86_64/tests/test_secrets.py
    - installers/linux-x86_64/tests/test_compose_template.py
    - installers/linux-x86_64/tests/test_state.py
    - installers/linux-x86_64/tests/test_distro.py
    - installers/linux-x86_64/tests/test_health.py
    - installers/linux-x86_64/tests/test_filesystem.py
  modified:
    - BitNest/Program.cs
decisions:
  - Changed Program.Main to async Task Main to support await in startup admin seeding block
  - Admin seeding skipped silently when either env var is empty — no log noise for non-installer deployments
  - Tests use sys.path.insert to locate install.py without installing as a package
  - get_docker_install_commands for apt_ubuntu/apt_debian share the same command list (same Docker CE repo path)
metrics:
  duration: 250s
  completed: "2026-03-26T10:29:38Z"
  tasks_completed: 2
  files_modified: 12
---

# Phase 10 Plan 01: API Admin Seeding + Installer Core Logic Summary

**One-liner:** Admin startup seeding via BITNEST_ADMIN_USER/PASS env vars plus all pure-function installer logic (validation, secrets, compose template, state file, distro detection, health polling, filesystem ops) with 67-test pytest suite covering all functions.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | API admin seeding on startup | a4b1e28 | BitNest/Program.cs |
| 2 | Installer core logic functions + test suite | 29e69d4 (GREEN), d63ccd8 (RED) | installers/linux-x86_64/install.py, tests/* |

## What Was Built

### Task 1: API Admin Seeding (BitNest/Program.cs)

Modified `Program.Main` from `void` to `async Task` to support the async seeding block. After the EF Core migration block, the API now:

1. Reads `BITNEST_ADMIN_USER` and `BITNEST_ADMIN_PASS` from environment variables.
2. If both are non-empty, opens a new DI scope, checks `!await seedDb.Users.AnyAsync()`.
3. If the users table is empty, calls `CreateUserAsAdminAsync(adminUser, adminPass, isAdmin: true)`.
4. Logs success at Information level, failure at Warning level.
5. Silently skips if either env var is absent.

### Task 2: Installer Core Logic (installers/linux-x86_64/install.py)

Created `install.py` with all business logic functions structured above the `# --- TUI --- #` marker, ready for Plan 02 TUI wiring:

**Validation:** `validate_port`, `validate_path`, `validate_username`, `validate_password` — each returns `(bool, str)` tuple with exact error messages per UI-SPEC copywriting contract.

**Preflight:** `is_port_free` (socket.bind), `check_disk_space` (shutil.disk_usage), `check_docker` (shutil.which + subprocess), `check_compose_version`.

**Secrets:** `generate_secret` — `secrets.token_hex(32)` producing 64-char hex-only strings per D-23.

**Compose template:** `COMPOSE_TEMPLATE` constant with all required fields:
- `pg_isready -U bitnest -d bitnest` healthcheck on db service (D-17)
- `condition: service_healthy` on api depends_on (D-17)
- `restart: unless-stopped` on all 3 services (D-18)
- `BITNEST_ADMIN_USER` and `BITNEST_ADMIN_PASS` on api (D-19)
- `denedi22/bitnest_api:latest` and `denedi22/bitnest_frontend:latest` (D-13)
- `${DATA_DIR}` bind mounts (using `${{DATA_DIR}}` in Python source) (D-16)

**Filesystem:** `write_env_file` (writes DATA_DIR, POSTGRES_PASSWORD, AUTH_SIGNING_KEY, BITNEST_ADMIN_USER, BITNEST_ADMIN_PASS, then chmod 600), `create_install_dirs` (data/storage + data/postgres).

**State file:** `state_path`, `write_state`, `read_state` — XDG-compliant at `~/.config/bitnest/install.json` per D-20.

**Distro detection:** `read_os_release` (parse /etc/os-release), `get_docker_install_path` (returns "apt_ubuntu", "apt_debian", "dnf_fedora", "dnf_rhel", "pacman", or "fallback").

**Docker install commands:** `get_docker_install_commands` returns per-distro command lists.

**Health polling:** `parse_compose_ps_json` (parses JSON lines, healthy if Health=="healthy" OR State=="running" AND Health==""), `poll_health` (60s timeout loop).

**Subprocess:** `run_cmd` wrapper — never shell=True, never cwd=.

### Test Suite (67 tests, all passing)

| File | Tests | Coverage |
|------|-------|---------|
| test_validation.py | 12 | port/path/username/password validators |
| test_preflight.py | 5 | port binding, disk space, docker detection |
| test_secrets.py | 3 | length, hex-only, uniqueness |
| test_compose_template.py | 15 | valid YAML, all required fields, ports, images |
| test_state.py | 4 | roundtrip, XDG path, missing file returns None |
| test_distro.py | 9 | ubuntu/debian/fedora/arch/rhel/unknown/ID_LIKE |
| test_health.py | 7 | healthy/running/exited/unhealthy/starting/multi |
| test_filesystem.py | 5 | create dirs, chmod 600, env var presence/values |

## Deviations from Plan

### Auto-fixed Issues

None — plan executed exactly as written.

## Known Stubs

None — all functions are fully implemented. The `# --- TUI --- #` section is a marker for Plan 02, not a stub.

## Verification Results

- `dotnet build BitNest/BitNest.csproj --no-restore` — 0 errors, 10 warnings (pre-existing)
- `python -m pytest installers/linux-x86_64/tests/ -x -q` — 67 passed
- `grep -c "BITNEST_ADMIN_USER" BitNest/Program.cs` — 1
- `grep -c "COMPOSE_TEMPLATE" installers/linux-x86_64/install.py` — 2
- `grep -c "pg_isready" installers/linux-x86_64/install.py` — 1

## Self-Check: PASSED

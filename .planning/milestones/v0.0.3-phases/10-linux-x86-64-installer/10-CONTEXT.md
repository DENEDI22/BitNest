# Phase 10: Linux x86_64 Installer - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver a single Python script (`installers/linux-x86_64/install.py`) that takes a user from a bare Linux x86_64 machine to a fully running BitNest Docker Compose stack. The script handles Docker installation, guided configuration, image pull, stack startup, admin account creation, and provides update/uninstall flows. This phase establishes ALL shared patterns (TUI structure, compose template, state schema, subprocess wrapper) that ARM64 and WSL2 variants will be derived from.

Not in this phase: ARM64-specific Docker repo config, WSL2 Docker Desktop guidance, PyInstaller bundling, CI release pipeline.

</domain>

<decisions>
## Implementation Decisions

### TUI Structure
- **D-01:** Script always opens a main menu (Textual app) with three options: Install BitNest / Update BitNest / Uninstall BitNest. User navigates with ↑↓, selects with Enter.
- **D-02:** On startup, check `~/.config/bitnest/install.json`. If it exists (prior install detected), skip to main menu with "Update BitNest" pre-highlighted. If it does not exist, pre-highlight "Install BitNest".
- **D-03:** Install wizard is 4 steps with a persistent step indicator at top: Step 1/4 Prerequisites → Step 2/4 Configuration → Step 3/4 Installing → Step 4/4 Done.
- **D-04:** Back/Next navigation between steps. User can go Back from Step 2; Steps 3 and 4 are non-interactive (progress + result only).

### Step 1 — Prerequisites
- **D-05:** Check and display status for: Docker Engine (installed/missing), Docker Compose V2 plugin (`docker compose version`), port availability (5000 and 3000 by default), disk space (warn if <5 GB free). Each check shows ✔ (pass) or ✗ (fail/action needed).
- **D-06:** If Docker is missing, show "Docker will be installed automatically" with ✗ replaced by a pending indicator. Actual Docker install happens when user advances to Step 3 (not during Step 1 check).
- **D-07:** Port conflicts detected in Step 1 are surfaced as ✗ with a note. User can change ports in Step 2 — re-validation happens on Next from Step 2.

### Step 2 — Configuration
- **D-08:** Prompts in Step 2 (in order):
  1. Install directory (default: `~/bitnest`, validated and expanded to absolute path)
  2. API port (default: `5000`, integer validation)
  3. Frontend port (default: `3000`, integer validation)
  4. Admin username (no default, required, min 3 chars)
  5. Admin password (no default, required, min 8 chars, masked input)
- **D-09:** DB password and JWT signing key are auto-generated silently using `secrets.token_hex(32)`. NOT shown to user during wizard (they are in the `.env` file).
- **D-10:** Admin credentials (username + password) are written to `.env` as `BITNEST_ADMIN_USER` and `BITNEST_ADMIN_PASS`. The API must consume these env vars to seed the admin account on first startup if no users exist yet. This requires a small addition to the API startup logic (check for env vars, create admin user if DB is empty).

### Step 3 — Installing
- **D-11:** Step 3 sequence (all shown with live status lines):
  1. Install Docker if missing (sudo escalation only for this step, using distro-detected package manager: apt → dnf/yum → pacman → get.docker.com fallback)
  2. Add current user to docker group (`sudo usermod -aG docker $USER`); use `sudo docker` for all remaining compose calls in this session
  3. Create install dir and subdirs (`data/storage/`, `data/postgres/`)
  4. Write `compose.yaml` (embedded template, bind mounts, pg_isready healthcheck)
  5. Write `.env` (chmod 600 immediately after creation)
  6. `docker compose -f <abs_path> pull` with animated spinner (images can take minutes)
  7. `docker compose -f <abs_path> up -d`
  8. Health poll loop (60s timeout): check `docker compose ps` per-service until all healthy

### Step 4 — Done (Success Screen)
- **D-12:** Success screen shows:
  - ✔ All services healthy
  - Frontend URL: `http://localhost:{frontend_port}`
  - API URL: `http://localhost:{api_port}`
  - "Your admin account has been created. Open your browser to get started."
  - Admin username (displayed), admin password NOT displayed again
  - "Run this installer again to update or uninstall BitNest."

### Docker Hub Images
- **D-13:** Docker Hub username is `denedi22`. Image references hardcoded as:
  - `denedi22/bitnest_api:latest`
  - `denedi22/bitnest_frontend:latest`
  - Database: `postgres:16` (official image)
- **D-14:** Always pull `:latest`. No version pinning in v0.1.0.

### compose.yaml Template
- **D-15:** Embedded in the Python script as a string constant using `str.format()` (NOT `string.Template` — conflicts with Docker's `${VAR}` syntax). Python variables use `{python_var}`, Docker compose variables use `${{DATA_DIR}}` in Python source (produces `${DATA_DIR}` in output).
- **D-16:** Uses bind mounts (not named Docker volumes) so users can find/backup their data:
  - `{install_dir}/data/storage` → `/app/data/storage` (API files)
  - `{install_dir}/data/postgres` → `/var/lib/postgresql/data` (DB)
- **D-17:** Includes pg_isready healthcheck on the db service and `condition: service_healthy` on the api's depends_on. This prevents the API/DB race condition. MANDATORY in the template.
- **D-18:** All services have `restart: unless-stopped`.
- **D-19:** Admin seed env vars included in compose template: `BITNEST_ADMIN_USER: ${BITNEST_ADMIN_USER}` and `BITNEST_ADMIN_PASS: ${BITNEST_ADMIN_PASS}` on the api service.

### State File
- **D-20:** Written to `~/.config/bitnest/install.json` after successful install. Format:
  ```json
  {
    "install_dir": "/home/user/bitnest",
    "api_port": 5000,
    "frontend_port": 3000,
    "compose_file": "/home/user/bitnest/compose.yaml",
    "installed_at": "2026-03-26T00:00:00Z"
  }
  ```
  Secrets (DB password, JWT key, admin password) are NOT stored in the state file.

### Update Flow
- **D-21:** Update flow (from main menu): read state file → `docker compose -f {compose_file} pull` with spinner → `docker compose -f {compose_file} up -d` → health poll → show result. No `docker compose down`.

### Uninstall Flow
- **D-22:** Uninstall flow: read state file → confirm screen ("This will stop BitNest. Continue?") → `docker compose -f {compose_file} down` → second confirm screen with red warning ("Delete all data including files and database? This cannot be undone.") → if yes: `shutil.rmtree(install_dir)` → delete state file.

### Secret Generation
- **D-23:** Use `secrets.token_hex(32)` for DB password and JWT signing key. Hex-only output — no special characters that could break `.env` interpolation.

### Subprocess Pattern
- **D-24:** All docker compose calls use `subprocess.run(["docker", "compose", "-f", abs_compose_path, ...], check=True)`. Never use `cwd=` (compose resolves .env relative to -f path). Never use `shell=True`. Sudo only for Docker install and usermod steps.

### Claude's Discretion
- Exact Textual widget types (Input, Button, Label, ProgressBar, etc.) and layout composition
- Exact ANSI color scheme / Textual CSS
- Spinner animation implementation during docker pull
- Exact distro detection logic (reading `/etc/os-release` ID and ID_LIKE fields)
- Health poll implementation details (polling interval, per-service status parsing)

</decisions>

<specifics>
## Specific Ideas

- Main menu preview: three options with ▶ selector, ↑↓/Enter navigation, Q to quit
- 4-step wizard with persistent "Step X/4" indicator at top of screen
- Admin password field uses masked input (Textual's `Input(password=True)`)
- Docker group re-login problem solved by using `sudo docker compose` for all compose calls in the current session (not by calling newgrp or re-exec)
- Distro detection via `/etc/os-release`: read `ID` and `ID_LIKE` fields to pick apt/dnf/pacman
- Raspberry Pi OS reports `ID=debian` in `/etc/os-release` — same apt path as Debian

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project & Phase Scope
- `.planning/REQUIREMENTS.md` — Requirements INST-01 through INST-12, LINUX-01, LINUX-02 (all in this phase)
- `.planning/ROADMAP.md` §Phase 10 — Goal, success criteria, dependencies
- `.planning/research/SUMMARY.md` — Full research synthesis: stack patterns, feature table stakes, architecture decisions, pitfalls

### Research Detail
- `.planning/research/STACK.md` — Exact Python stdlib modules, Docker install commands per distro, WSL2 detection, Raspberry Pi notes
- `.planning/research/ARCHITECTURE.md` — Single-file architecture, compose embedding pattern (str.format vs string.Template), state file schema, subprocess wrapper pattern, build order rationale
- `.planning/research/FEATURES.md` — Table stakes vs differentiators, update flow patterns (no docker compose down), uninstall two-phase confirmation, state file linchpin
- `.planning/research/PITFALLS.md` — 12 pitfalls with prevention: docker group session, .env special chars, tilde path expansion, DB race condition, port pre-flight order

### Existing App
- `compose.yaml` — Current service names, port mappings, and volume configuration (reference for template)
- `BitNest/appsettings.json` — Auth config keys (`Auth__SigningKey`, `ConnectionStrings__DefaultConnection`, `UploadsPath`) that become env vars in installer compose template
- `.github/workflows/docker-image.yml` — Confirms image name pattern (`denedi22/bitnest_api:latest`, `denedi22/bitnest_frontend:latest`) and multi-arch build

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — `installers/` directory does not exist yet. Phase 10 creates it from scratch.

### Established Patterns
- Docker Compose service names: `api` (port 8080 internal → 5000 external), `db` (postgres:16), `frontend` (port 80 internal → 3000 external)
- API env vars: `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection`, `UploadsPath`, `Auth__SigningKey`
- DB env vars: `POSTGRES_DB=bitnest`, `POSTGRES_USER=bitnest`, `POSTGRES_PASSWORD`
- Admin seed vars (new, to be added to API): `BITNEST_ADMIN_USER`, `BITNEST_ADMIN_PASS`

### Integration Points
- API must be extended to read `BITNEST_ADMIN_USER` / `BITNEST_ADMIN_PASS` env vars on startup and seed an admin account if no users exist. This is a small addition to the existing auth/user system (Phase 6/7 work).
- The `.env` file and `compose.yaml` live in the user's install directory, NOT in the repo root.

</code_context>

<deferred>
## Deferred Ideas

- Version pinning (`--version` flag) — deferred to v2
- `--non-interactive` / `--yes` flags — Phase 13
- PyInstaller bundling and build spec — Phase 13
- GitHub Actions release pipeline — Phase 13
- ARM64-specific Docker repo config (`arch=arm64`) — Phase 11
- WSL2 Docker Desktop guidance — Phase 12
- Backup `.env` before update — v2 requirement
- Systemd service unit for auto-start — v2 requirement

</deferred>

---

*Phase: 10-linux-x86-64-installer*
*Context gathered: 2026-03-26*

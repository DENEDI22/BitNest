# Feature Research

**Domain:** Python installer scripts for self-hosted Docker Compose applications
**Researched:** 2026-03-26
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume every competent installer provides. Missing any of these causes instant distrust.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Prerequisite check at startup | Users expect to know if the system is ready before any changes are made | LOW | Check Python version, Docker presence, Docker daemon running, available disk space, architecture detection (x86_64 / arm64) |
| Auto-install Docker when absent | Linux users expect the installer to set up everything; manual steps break trust | MEDIUM | Use Docker's official `get.docker.com` script; skip if already installed; detect distro (Debian/Ubuntu/RHEL family); WSL2 requires Docker Desktop guidance not engine install |
| Interactive config wizard for required secrets | No user should hand-edit a raw `.env` file; the wizard owns config generation | MEDIUM | Wizard prompts for: install directory, port, admin email; auto-generates JWT secrets, DB password, DB name with `secrets.token_urlsafe(32)` from stdlib |
| Write composed `.env` file to install directory | Expected standard for Docker Compose deployments | LOW | File must be adjacent to `docker-compose.yml`; never committed to VCS |
| `docker compose pull` + `docker compose up -d` orchestration | Users expect the installer to start the stack, not stop at config | MEDIUM | Sequence: pull images, up detached; pull can take 2-5 min on first run |
| Post-install health verification before declaring success | "done" printed before services respond destroys trust | MEDIUM | Poll `docker compose ps` or container health status in a loop; report per-service status |
| Clear success message with access URL | Users need to know where to go after install | LOW | Print `http://localhost:<port>` or `http://<host>:<port>`; tell user to open browser |
| Idempotent re-run | Running install twice must not corrupt a working install or lose data | MEDIUM | Detect existing state file; branch to update flow or abort with clear message |
| Graceful error messages with remediation hints | Cryptic Python tracebacks kill trust; installer must explain failures in plain English | LOW | Catch subprocess failures; print the failing command, exit code, and a remediation hint |
| Exit codes that reflect success/failure | Required for scripting and CI use | LOW | `sys.exit(0)` on success, non-zero on failure; use consistent codes |

### Differentiators (Competitive Advantage)

Features that set this installer above a raw `docker compose up` guide in a README.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Spinning/animated progress during `docker pull` | Pulling multi-arch images takes 2-5 min; silence causes users to ctrl-C | LOW | Use stdlib `threading` + `itertools.cycle` for a spinner on a background thread; no third-party deps |
| Per-step status line ("Pulling images... done") | Lets users track exactly where they are in a multi-step process | LOW | Print step header, run operation, overwrite or follow with checkmark; use ANSI green on success |
| State file written after install | Enables `update` and `uninstall` to find the install without asking again | LOW | JSON file at `~/.config/bitnest/state.json`; stores `install_dir`, `compose_file`, `version`, `installed_at`, `port` |
| Auto-detect existing install from state file | `update` and `uninstall` commands work without arguments | LOW | Read state file first; fall back to prompting if missing |
| Minimal-downtime update flow | `update` should not delete volumes or config; just swap images | MEDIUM | Sequence: pull new images, `docker compose up -d --no-deps --build <service>` per service, verify health; avoid `docker compose down` which drops containers |
| Uninstall with explicit "keep data" prompt | Users who reinstall should not lose files; data destruction must be confirmed | LOW | Two-phase uninstall: always stop+remove containers; separately ask about volumes and install directory with clear warning |
| Arch-aware image pulling | ARM64 (Raspberry Pi) users need the right manifest | LOW | Detect `platform.machine()`: `x86_64` vs `aarch64`; pass `--platform linux/arm64` or let Docker pick via manifest if images are multi-arch |
| WSL2 Docker Desktop readiness check | Windows users most commonly fail because Docker Desktop is not running | LOW | On WSL2, check `docker info` succeeds; if not, print exact Docker Desktop start instructions |
| Dry-run mode (`--dry-run`) | Power users want to see what would happen without side effects | MEDIUM | Print each step without executing; useful for debugging in CI |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| `pip install` anything during installer run | Seems like an easy way to get rich CLI libraries (Click, Rich) | Violates stdlib-only constraint from PROJECT.md; requires network; fails on air-gapped hosts | Use only stdlib: `subprocess`, `threading`, `json`, `secrets`, `shutil`, `platform`, `argparse` |
| Auto-update installer itself on every run | Feels modern and always-current | Installs from network without user consent on every unrelated command; causes version drift surprises | Notify user when a new installer version exists; do not auto-update |
| Collect telemetry or phone-home | Seems useful for error tracking | Privacy violation for a self-hosted product whose whole appeal is data sovereignty | No telemetry, ever; local-only logging |
| Interactive prompts in `update` or `uninstall` paths | Feels safe to confirm everything | Breaks automation and cron-job usage; users re-running scripts expect `--yes` to silence prompts | Require explicit `--yes` flag for non-interactive mode; prompt only in interactive TTY |
| `docker compose down` in update flow | Seems clean to tear down before rebuilding | `down` removes containers and may drop anonymous volumes; causes full downtime | Use `docker compose up -d` with pulled images; `down` only in full uninstall |
| Hardcoded ports (80/443) | "Standard" ports | Conflicts with existing services on home-lab machines (nginx, Caddy, other apps) | Always prompt for port with a sensible default (e.g. 8080); validate the port is free |
| Storing secrets in the state file | Convenient for future operations | State file may have broad read permissions; leaks DB password and JWT secret | State file stores only install metadata (directory, port, version); secrets live only in `.env` adjacent to `docker-compose.yml` |
| Silently overwriting `.env` on re-run | Feels like "fix it" | Destroys user customizations and rotates secrets, invalidating existing sessions and DB auth | On re-run: detect existing `.env`; offer to keep, diff, or regenerate; never silently overwrite |
| Parallel container startup without waiting for healthy | Looks faster | Nginx serves 502 if API is not up; user sees broken app and assumes install failed | Start db first, wait for healthy, then api, wait for healthy, then frontend |

## Feature Dependencies

```
[update command]
    └──requires──> [state file] (to know where install lives)
                       └──requires──> [install command ran successfully]

[uninstall command]
    └──requires──> [state file] (to know what to remove)

[post-install health check]
    └──requires──> [docker compose up completed]
                       └──requires──> [docker compose pull completed]
                                          └──requires──> [Docker daemon running]
                                                             └──requires──> [Docker installed]

[arch-aware pull]
    └──requires──> [platform.machine() detection] (stdlib, no deps)

[WSL2 Docker Desktop check]
    └──requires──> [WSL2 environment detection] (check /proc/version for Microsoft kernel string)

[config wizard]
    └──requires──> [install directory selected]
    └──enhances──> [.env file generation]

[.env file generation]
    └──requires──> [config wizard or --non-interactive flags]
    └──conflicts──> [silently overwriting existing .env]
```

### Dependency Notes

- **State file required before update/uninstall:** Without it, the script does not know where to find `docker-compose.yml` or which containers to target. If missing, prompt user for install directory and recreate.
- **Health check requires compose up:** The poll loop must only start after `docker compose up -d` exits 0. Do not poll before the command runs.
- **db must be healthy before api starts:** `depends_on: db: condition: service_healthy` in `docker-compose.yml` handles this at the Compose level, but the installer's health polling must wait for all three services, not just the first one up.
- **Docker install requires root/sudo:** On Linux, Docker engine installation requires elevated privileges. Installer must detect if running as root or if `sudo` is available; fail with instructions if neither.

## MVP Definition

### Launch With (v1 — this milestone)

- [ ] Prerequisites check (Python version, Docker present + running, disk space, arch detection) — without this users get cryptic failures
- [ ] Docker auto-install for Linux x86_64 and ARM64 via `get.docker.com` — core value proposition
- [ ] WSL2 Docker Desktop readiness check (no auto-install on WSL2) — Windows path blocker
- [ ] Interactive config wizard: port, install directory, admin email; auto-generate secrets — cannot run without this
- [ ] Generate `docker-compose.yml` + `.env` in install directory — required for compose orchestration
- [ ] Pull images with spinner progress feedback — without progress, `docker pull` silence causes users to abort
- [ ] `docker compose up -d` with ordered startup — core install action
- [ ] Post-install health poll loop: wait up to 60s for all containers healthy — required before printing success
- [ ] State file written to `~/.config/bitnest/state.json` — required for update/uninstall
- [ ] `update` command: pull new images, rolling up per service, verify health — core update flow
- [ ] `uninstall` command: stop+remove containers, prompt for data removal — core uninstall flow
- [ ] Per-step status lines and clear success/failure messages — installer is unusable without feedback

### Add After Validation (v1.x)

- [ ] `--non-interactive` / `--yes` flags for automation/CI use — once basic flows are validated
- [ ] Dry-run mode — after basic install is trusted
- [ ] Backup `.env` and compose file before update — safety net once update flow is exercised
- [ ] Version check: compare installed version tag against Docker Hub latest tag — enables update notifications

### Future Consideration (v2+)

- [ ] TLS/HTTPS setup via Caddy or Certbot integration — significant additional complexity; out of scope for v0.1.0
- [ ] Systemd service unit to auto-start on boot — requires root-level integration; separate concern
- [ ] GUI wizard (Textual/Urwid) — stdlib-only constraint means terminal-only for now

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Prerequisites check | HIGH | LOW | P1 |
| Docker auto-install (Linux) | HIGH | MEDIUM | P1 |
| Config wizard + .env generation | HIGH | MEDIUM | P1 |
| Spinner during docker pull | HIGH | LOW | P1 |
| Post-install health check loop | HIGH | MEDIUM | P1 |
| State file | HIGH | LOW | P1 |
| Update command (rolling, no downtime) | HIGH | MEDIUM | P1 |
| Uninstall with keep-data prompt | HIGH | LOW | P1 |
| WSL2 Docker Desktop guidance | HIGH | LOW | P1 |
| Per-step status feedback | HIGH | LOW | P1 |
| --non-interactive flag | MEDIUM | LOW | P2 |
| Dry-run mode | MEDIUM | MEDIUM | P2 |
| Version check / update notification | MEDIUM | LOW | P2 |
| Backup before update | MEDIUM | LOW | P2 |
| TLS/HTTPS integration | MEDIUM | HIGH | P3 |
| Systemd service unit | LOW | MEDIUM | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Detailed UX Patterns

### Config Wizard: What to Prompt vs Auto-Generate vs Hardcode

| Setting | Disposition | Rationale |
|---------|-------------|-----------|
| Install directory | PROMPT with default `~/bitnest` | User may want it on a different mount/disk; must be explicit |
| Port | PROMPT with default `8080` | Home-lab machines commonly have port conflicts; hardcoding is the #1 installer complaint |
| Admin email | PROMPT | Required for first-user bootstrap in BitNest |
| DB password | AUTO-GENERATE `secrets.token_urlsafe(32)` | No user benefit from choosing this; auto-gen is more secure |
| JWT secret | AUTO-GENERATE `secrets.token_urlsafe(48)` | Same rationale; must be long and random |
| DB name | HARDCODE `bitnest` | No reason to vary; shown in summary so user can see it |
| DB user | HARDCODE `bitnest` | Same rationale |
| Image tag | HARDCODE `latest` at install; pinned tag stored in state file post-pull | Install always uses latest; update flow respects pinned tag |
| Container network name | HARDCODE `bitnest_net` | Internal detail; no user benefit from choosing |

### Post-Install Health Check Approach

Recommended pattern for the installer's poll loop (stdlib only, no dependencies):

1. After `docker compose up -d` exits 0, enter a polling loop.
2. Run `docker compose ps --format json` (Docker Compose v2) or parse `docker ps` output.
3. Check all three services (`api`, `db`, `frontend`) report `Status: healthy` or `running`.
4. If all healthy within 60 seconds: print success. If timeout: print which container is still not healthy, show `docker compose logs <service>` hint.
5. For the API specifically: also do an HTTP GET to `http://localhost:<port>/health` (or a known API endpoint) with `urllib.request` (stdlib). A 200 response confirms the app layer is up, not just the container process.

Confidence: HIGH — `docker compose ps --format json` is available in Docker Compose v2.x (which ships with Docker Engine 23+, the minimum for multi-arch images). The `--wait` flag on `docker compose up` is a simpler alternative but does not give per-service feedback.

### Update Flow (Minimal Downtime for Single-Node Docker Compose)

True zero-downtime requires a load balancer and multiple replicas — not realistic for a home-lab single-node stack. The realistic goal is **minimal downtime** (seconds, not minutes).

Recommended sequence:
1. `docker compose pull` — pulls new layers while old containers keep running (no downtime during pull)
2. `docker compose up -d --no-deps api` — replaces API container only; Compose stops old, starts new
3. Poll API health (up to 30s)
4. `docker compose up -d --no-deps frontend` — replaces Nginx container (fast, seconds)
5. Skip `db` restart unless version changed — PostgreSQL does not change on BitNest image updates
6. Print per-service result

Note: `docker compose down` must NOT be used in the update path — it removes containers and can drop anonymous volumes. Use `up -d` with pulled images only.

### Uninstall Flow: What to Preserve vs Destroy

| Item | Default | With `--purge` flag |
|------|---------|---------------------|
| Running containers | STOP + REMOVE (always) | STOP + REMOVE (always) |
| Docker images | KEEP (user may have other uses) | REMOVE with `docker image rm` |
| Named volumes (DB data, file chunks) | KEEP with explicit prompt and warning | REMOVE only after double confirmation |
| `.env` file | KEEP (contains user config) | REMOVE |
| `docker-compose.yml` | KEEP | REMOVE |
| Install directory if empty | KEEP | REMOVE |
| State file (`~/.config/bitnest/state.json`) | REMOVE (install is gone) | REMOVE |

### State File Format

Location: `~/.config/bitnest/state.json` (Linux/WSL2). Create parent dir with `os.makedirs(..., exist_ok=True)`.

```json
{
  "version": "1",
  "installed_at": "2026-03-26T14:32:00Z",
  "updated_at": "2026-03-26T14:32:00Z",
  "install_dir": "/home/user/bitnest",
  "compose_file": "/home/user/bitnest/docker-compose.yml",
  "port": 8080,
  "image_tag": "latest",
  "platform": "linux-x86_64"
}
```

Fields NOT stored in state file: DB password, JWT secret, admin email — secrets stay in `.env` only.

## Competitor / Reference Installer Analysis

| Aspect | Coolify installer (curl | bash) | Plausible CE (manual docker compose) | BitNest target |
|--------|-------------------------------|---------------------------------------|----------------|
| Prompts | None — zero interactive prompts | None — user edits .env manually | Interactive wizard for port, directory, email |
| Docker install | Yes, auto-installs Docker engine | No — user must have Docker | Yes, auto-install on Linux |
| Progress feedback | Minimal | None | Spinner + per-step status |
| Post-install verification | Prints URL, no health check | None | Poll loop + HTTP probe |
| State file | No | No | Yes — `~/.config/bitnest/state.json` |
| Update command | Built-in (self-update mechanism) | Manual `docker compose pull && up` | `update` sub-command |
| Uninstall command | Separate uninstall script | Not provided | `uninstall` sub-command with keep-data prompt |

## Sources

- [Docker Compose Health Checks Guide — Last9](https://last9.io/blog/docker-compose-health-checks/)
- [Docker Compose Health Checks — Practical Guide](https://www.tvaidyan.com/2025/02/13/health-checks-in-docker-compose-a-practical-guide/)
- [docker compose up --wait flag — Docker Docs](https://docs.docker.com/reference/cli/docker/compose/up/)
- [Zero-Downtime Docker Compose — docker-rollout](https://github.com/wowu/docker-rollout)
- [Zero-Downtime Deployments with Docker Compose — Max Countryman](https://www.maxcountryman.com/articles/zero-downtime-deployments-with-docker-compose)
- [Coolify Installation Docs](https://coolify.io/docs/get-started/installation)
- [Plausible Community Edition](https://github.com/plausible/community-edition)
- [CLI UX Patterns — Lucas F. Costa](https://www.lucasfcosta.com/blog/ux-patterns-cli-tools)
- [CLI UX Best Practices: Progress Displays — Evil Martians](https://evilmartians.com/chronicles/cli-ux-best-practices-3-patterns-for-improving-progress-displays)
- [Docker Volumes and Uninstall Best Practices — DigitalOcean](https://www.digitalocean.com/community/tutorials/how-to-remove-docker-images-containers-and-volumes)
- [Coolify Uninstallation Docs](https://coolify.io/docs/get-started/uninstallation)
- [Docker system prune — Docker Docs](https://docs.docker.com/reference/cli/docker/system/prune/)
- [Wait for Container Dependencies — Docker Compose](https://oneuptime.com/blog/post/2026-01-25-wait-for-container-dependencies-docker-compose/view)

---
*Feature research for: BitNest v0.1.0 Python installer scripts (Linux x86_64, ARM64, Windows WSL2)*
*Researched: 2026-03-26*

# Project Research Summary

**Project:** BitNest v0.1.0 — Distribution & Installer Milestone
**Domain:** Python stdlib-only installer scripts for self-hosted Docker Compose applications
**Researched:** 2026-03-26
**Confidence:** HIGH

## Executive Summary

BitNest needs three standalone Python installer scripts — Linux x86_64, Linux ARM64, and Windows WSL2 — that take a user from a bare machine to a running Docker Compose stack without requiring any dependencies beyond Python 3.8 and an internet connection. This is a well-understood domain with documented patterns from `get-pip.py`, `rustup-init`, and Coolify's installer. The correct architecture is one self-contained file per platform, using only stdlib modules, with argparse subcommands dispatching to `do_install`, `do_update`, and `do_uninstall` functions. The compose.yaml is embedded as a string constant using `str.format()` substitution — never `string.Template`, which conflicts with Docker's `${VAR}` syntax.

The recommended approach builds the Linux x86_64 installer first to freeze all shared patterns (argparse structure, state file schema, compose template, .env writer, subprocess wrapper), then derives the ARM64 installer from it by copy, and develops the WSL2 installer independently since its Docker detection strategy is fundamentally different. All three installers share: `secrets.token_hex(32)` for secret generation, bind mounts over named volumes for transparent user data access, a JSON state file at `~/.config/bitnest/install.json`, and `docker compose` (V2 plugin, space form) as the exclusive compose command. State files must not store secrets; `.env` must be written `chmod 600` immediately after creation.

The critical risks are all implementation-level, not design-level: the Docker group membership problem (solved by using `sudo` for compose calls in the same session or re-executing via `sg docker`); special characters in generated passwords corrupting `.env` interpolation (solved by using `token_hex` which produces hex-only output); tilde paths in compose.yaml not expanding at runtime (solved by calling `os.path.expanduser` + `os.path.abspath` before writing); ARM64 images missing from Docker Hub (a CI gate, not an installer concern); and the API/DB startup race condition (solved by including `pg_isready` healthcheck with `condition: service_healthy` in the embedded compose template). All of these pitfalls are preventable at initial implementation with the right patterns — they become expensive to fix post-deployment.

---

## Key Findings

### Recommended Stack

The stack is entirely Python stdlib — no pip, no virtualenv, no third-party libraries. This is a hard constraint from the project design, not a stylistic preference: the installer must run on systems where pip may be absent or broken. The constraint is workable because the stdlib provides every needed primitive. See `.planning/research/STACK.md` for full module-by-module rationale and distro-specific Docker install command sequences.

**Core technologies:**
- `Python 3.8+` — installer runtime; 3.8 is the oldest Python on Debian Buster and later; all required modules available
- `subprocess` (stdlib) — system command execution; `subprocess.run(..., check=True)` with list arguments (never `shell=True`) for all docker, apt, dnf, pacman, and systemctl calls
- `secrets.token_hex(32)` — secret generation; cryptographically secure via `os.urandom`; hex-only output avoids `.env` interpolation hazards entirely
- `pathlib` (stdlib) — install directory creation, compose file paths, state file paths; `Path.mkdir(parents=True, exist_ok=True)` is idempotent
- `json` (stdlib) — state file read/write and parsing `docker info` output
- `platform` (stdlib) — `platform.machine()` returns `x86_64` or `aarch64`; drives ARM64-specific Docker repo configuration
- `argparse` (stdlib) — `add_subparsers()` with `set_defaults(func=...)` dispatch for `install`, `update`, `uninstall` subcommands
- `threading` + `itertools.cycle` (stdlib) — animated spinner on background thread during `docker compose pull`; prevents users from aborting a 2-5 minute silent image download

**Critical version requirements:**
- Python 3.8 minimum (f-strings, `secrets`, `subprocess.run`, `pathlib` all available)
- Docker Engine 20.10+ for Compose V2 plugin support; Docker 23.0+ bundles it by default
- `docker compose` (space, V2 plugin) exclusively; `docker-compose` (hyphen, V1) is EOL December 2023 and absent from Docker's official repos

### Expected Features

The research distinguishes clearly between what users expect on day one, what provides competitive advantage, and what must be deferred. See `.planning/research/FEATURES.md` for dependency graph, UX pattern tables, and competitor analysis (Coolify, Plausible CE).

**Must have (table stakes):**
- Prerequisites check at startup (Python version, Docker presence, daemon running, disk space, architecture) — users expect to know if the system is ready before any changes are made
- Docker auto-install on Linux (x86_64 and ARM64) via distro-specific package manager — the core value proposition; manual steps break trust
- WSL2 Docker Desktop readiness check and guided setup (no Docker Engine auto-install on WSL2) — Windows path blocker
- Interactive config wizard: install directory, port, admin email; auto-generate DB password and JWT secret — no user should hand-edit a raw `.env`
- Generate and write `compose.yaml` and `.env` to install directory — required for compose orchestration
- `docker compose pull` with animated spinner — silence during 2-5 minute image download causes users to abort
- `docker compose up -d` with ordered startup (DB healthcheck condition before API, then frontend)
- Post-install health poll loop (60s timeout, per-service status, HTTP probe on API endpoint)
- State file written to `~/.config/bitnest/install.json` after successful install
- `update` subcommand: pull new images, rolling `up -d --no-deps` per service, verify health
- `uninstall` subcommand: stop and remove containers, explicit prompt before deleting data volumes
- Per-step status lines and clear success/failure messages with remediation hints

**Should have (competitive differentiators):**
- `--non-interactive` / `--yes` flags for automation and cron use — add once basic flows are validated
- Dry-run mode (`--dry-run`) — after basic install is trusted
- Backup `.env` and compose file before update — safety net once update flow is exercised
- Version check: compare installed tag against Docker Hub latest

**Defer to v2+:**
- TLS/HTTPS via Caddy or Certbot — significant complexity; out of scope for v0.1.0
- Systemd service unit for boot auto-start — separate concern requiring root-level integration
- GUI wizard (Textual/Urwid) — stdlib-only constraint means terminal-only for now

**Anti-features to actively reject:**
- `pip install` anything during installer run — violates stdlib-only constraint
- Auto-updating the installer itself on every run — installs from network without user consent
- Telemetry or phone-home — privacy violation for a self-hosted product whose appeal is data sovereignty
- `docker compose down` in the update flow — removes containers and can drop anonymous volumes
- Silently overwriting `.env` on re-run — destroys user customizations and rotates secrets

### Architecture Approach

The architecture is deliberately simple: one self-contained Python file per platform. No shared module, no imports beyond stdlib, no directory structure for the user to clone. Each file embeds the compose.yaml template as a Python string constant using `str.format()` substitution (`{python_var}` for installer-time values, `${{DATA_DIR}}` in Python source to produce literal `${DATA_DIR}` in the output file for Docker Compose to resolve at runtime). Bind mounts are used over named volumes so users can locate, back up, and migrate their data without Docker internals knowledge. State lives at `~/.config/bitnest/install.json` (XDG Base Directory), separate from the install directory so uninstall cannot delete state before cleanup completes. See `.planning/research/ARCHITECTURE.md` for full component diagrams, data flow sequences, and anti-pattern documentation.

**Major components (within each installer file):**
1. CLI Layer (`build_parser()`) — argparse with `install`, `update`, `uninstall` subparsers; `set_defaults(func=...)` dispatch; no `if/elif` chains
2. Wizard Layer (`run_wizard()`) — plain `input()` prompts with validation; port conflict pre-flight before wizard begins; `secrets.token_hex` for generated secrets
3. File Writers (`write_compose_yaml()`, `write_env_file()`) — string template substitution; absolute paths only; `.env` written `chmod 600` immediately after creation
4. State Layer (`read_state()`, `write_state()`) — JSON at XDG config dir; no secrets stored; state deleted last during uninstall
5. Core Logic (`do_install()`, `do_update()`, `do_uninstall()`) — orchestrates steps; all docker compose calls go through a single wrapper
6. Subprocess Wrapper (`compose_run()`, `_compose_binary()`) — detects V2 plugin vs V1 standalone; always uses `-f <absolute_path>` not `cwd`; streams output for pull/up, captures for version checks

### Critical Pitfalls

The research identified 12 pitfalls. The top 5 by prevention cost and user impact are listed here; see `.planning/research/PITFALLS.md` for the full set including WSL2-specific and ARM64-specific issues.

1. **Docker group not active in same session** — After `usermod -aG docker`, the installer's own subprocess calls still run without the docker GID. Prevention: invoke all remaining docker compose calls via `sudo` for the current session, or re-execute the installer process via `os.execvp("sg", ["sg", "docker", "-c", ...])` with a re-exec guard flag. Never call `newgrp docker` via subprocess — it blocks.

2. **Special characters in generated passwords breaking `.env` interpolation** — Docker Compose performs shell-style `$` interpolation on `.env` values. Prevention: use `secrets.token_hex(32)` exclusively, which produces only `[0-9a-f]` characters with no interpolation-hazardous characters.

3. **Tilde and relative paths in compose.yaml volume mounts** — Docker Compose does not expand `~`; the container runtime receives and rejects it literally. Prevention: always call `os.path.expanduser()` + `os.path.abspath()` (or `Path.expanduser().resolve()`) on user-provided paths before writing to compose.yaml or `.env`.

4. **Container startup race condition — API before PostgreSQL is ready** — `depends_on` without a health condition only guarantees the DB container has started, not that PostgreSQL accepts connections. Prevention: embed a `pg_isready` healthcheck in the compose template from the start, with `condition: service_healthy` on the API's `depends_on`. This must be in the template string constant — users cannot be expected to add it manually.

5. **Port conflict discovered after the config wizard completes** — If port availability is not checked before the wizard, a bound port causes a cryptic Docker error at the very end of a multi-step flow. Prevention: run `socket.bind()` checks for all ports the stack will use before the first wizard prompt; re-prompt for an alternative port if any is taken.

---

## Implications for Roadmap

Based on the dependency graph in FEATURES.md and the build order recommendation in ARCHITECTURE.md, the natural phase structure for this milestone is:

### Phase 1: Linux x86_64 Installer Core
**Rationale:** All shared patterns are established here first. ARM64 and WSL2 variants are derived from or contrasted against this baseline. Building this first freezes the argparse structure, compose template, state schema, subprocess wrapper, and .env writer before any copying or divergence happens.
**Delivers:** Fully functional `install_linux_x86.py` with `install`, `update`, and `uninstall` subcommands; end-to-end flow from a bare machine to a running BitNest stack on Debian/Ubuntu/Fedora/RHEL/Arch.
**Addresses:** All P1 features from FEATURES.md — prerequisites check, Docker auto-install (multi-distro via `/etc/os-release` detection), config wizard, compose.yaml + .env generation, pull with spinner, health poll loop, state file, rolling update flow, uninstall with keep-data prompt.
**Avoids:** Docker group session pitfall (targeted `sudo` for compose calls or re-exec pattern), port conflict pre-flight (socket check before wizard, not after), tilde paths (expanduser before writing), .env interpolation (token_hex), DB race condition (healthcheck in template), root install file ownership (targeted escalation only, not whole-script sudo).

### Phase 2: Linux ARM64 Installer
**Rationale:** Derived from the x86_64 installer once its patterns are frozen. The differences are narrow but critical: `arch=arm64` in the apt source line, swap advisory based on `/proc/meminfo`, and multi-arch image verification before pull. Developing this after Phase 1 means there is a stable base to copy from.
**Delivers:** `install_linux_arm64.py` — functionally identical to x86_64 but with ARM64-specific Docker repo configuration and Raspberry Pi OS guidance (ID=debian path, bookworm codename, swap advisory for sub-2 GB RAM).
**Addresses:** ARM64 Docker repo pitfall (dynamic `arch=` based on `platform.machine()`), missing multi-arch image pitfall (manifest inspect before pull), Raspberry Pi OS Debian path.
**Note:** Requires the CI/GitHub Actions pipeline to publish `linux/arm64` manifests. This is a CI concern outside the installer's scope, but the installer must verify manifest availability and give a clear error if the CI gate has not been met.

### Phase 3: Windows WSL2 Installer
**Rationale:** Most architecturally distinct installer. Does not auto-install Docker Engine (guides to Docker Desktop instead), must handle the systemd-absent case, and must warn against `/mnt/c/` data paths for volume performance and PostgreSQL permission reasons. Can be developed in parallel with Phase 2 if two engineers are available — there is no technical dependency between them once Phase 1 patterns are established.
**Delivers:** `install_windows_wsl.py` — WSL2 environment detection via `/proc/sys/kernel/osrelease`, Docker Desktop readiness check with retry loop, wizard with path validation rejecting `/mnt/` filesystems, otherwise identical compose orchestration.
**Addresses:** WSL2 Docker Desktop not-running vs not-installed distinction (inspect socket error type, not binary presence), systemd absence (check `/run/systemd/private` before calling systemctl; use `service` or `dockerd` fallback), `/mnt/c/` volume performance and PostgreSQL permission failure (filesystem type check, warn and re-prompt).

### Phase 4: Automation Flags and Polish (v1.x)
**Rationale:** Once all three installers are validated through basic install/update/uninstall flows, add the automation surface. These are P2 features — high value for CI and scripting users, low implementation cost, but requiring the P1 flows to be trusted first.
**Delivers:** `--non-interactive`/`--yes` flags across all three installers; dry-run mode; `.env` backup before update; version check against Docker Hub latest tag.
**Addresses:** The "automation without prompts" requirement documented in FEATURES.md (interactive prompts in update/uninstall break cron jobs).

### Phase Ordering Rationale

- Phase 1 before Phase 2: Phase 2 is a diff on top of Phase 1. Copying before the original is stable creates two diverging codebases with no single source of truth.
- Phase 1 before Phase 3: WSL2 engineers need a reference implementation to diverge from, not a blank slate.
- Phase 3 can run parallel to Phase 2: the only dependency is "Phase 1 patterns are frozen," not "Phase 2 is complete."
- Phase 4 last: `--non-interactive` presupposes interactive flows are correct. Adding automation flags to a broken flow automates the breakage.
- Port conflict pre-flight (Pitfall 11) must be implemented in Phase 1 before the config wizard — not added later. Discovering port conflicts after the wizard completes is the single worst UX failure mode short of a broken install.
- The DB healthcheck pattern (Pitfall 10) must be embedded in the Phase 1 compose template string constant. It cannot be retrofitted without releasing a new installer version and requiring users to reinstall.

### Research Flags

Phases with straightforward, well-documented patterns — no additional research-phase needed:
- **Phase 1 (Linux x86_64):** Every pattern is backed by official Docker docs and Python stdlib docs. STACK.md provides exact distro detection code and apt/dnf/pacman command sequences ready to implement.
- **Phase 4 (Automation flags):** `argparse` `store_true` flags and TTY detection (`sys.stdout.isatty()`) are fully documented stdlib patterns.

Phases that may benefit from targeted research during planning:
- **Phase 2 (ARM64):** Multi-arch Docker Hub manifest publication requires verifying the current GitHub Actions pipeline's `build-push-action` configuration. PITFALLS.md (Pitfall 9) identifies this as a CI gate that could block Phase 2 user-acceptance testing even if the installer code is correct. Recommend verifying CI pipeline state before Phase 2 planning begins.
- **Phase 3 (WSL2):** WSL2 systemd detection (`/run/systemd/private` check) has MEDIUM confidence from a single community source. If the product decision shifts to supporting Docker Engine installation inside WSL2 (rather than guiding to Docker Desktop), this area needs deeper research on the `dockerd` direct-start fallback.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All core modules verified against official docs.python.org; Docker install commands fetched from official Docker docs 2026-03-26; ANSI readline cursor bug confirmed on CPython issue tracker |
| Features | HIGH | Patterns cross-validated against Coolify and Plausible CE reference implementations; Docker Compose health check behaviour verified against official docs; competitor analysis grounded in real installers |
| Architecture | HIGH | All patterns backed by official Docker Compose interpolation and environment variable docs; XDG Base Directory Specification; Python subprocess and argparse official docs |
| Pitfalls | HIGH | 11 of 12 pitfalls verified against official Docker docs or CPython issue tracker; one pitfall (WSL2 systemd detection path) has MEDIUM confidence from a community source |

**Overall confidence:** HIGH

### Gaps to Address

- **Multi-arch CI pipeline state:** PITFALLS.md Pitfall 9 flags that `docker manifest inspect` must confirm `linux/arm64` exists before the ARM64 installer declares success. Whether the current GitHub Actions pipeline publishes ARM64 manifests is unknown from installer research. This must be verified at Phase 2 planning time — it is a blocker for Phase 2 acceptance testing regardless of installer code correctness.

- **WSL2 systemd detection source quality:** The `/run/systemd/private` path check for detecting systemd presence is documented in one community article (MEDIUM confidence). The `dockerd` direct-start fallback in the absence of systemd has not been tested against Docker Desktop's WSL2 integration. This gap only matters if the product decision changes to supporting Docker Engine installation inside WSL2.

- **compose.yaml template completeness:** The compose template in ARCHITECTURE.md is illustrative. The actual service names, Docker Hub image references, exposed ports, and environment variable names must be confirmed against the existing `compose.yaml` at repo root and the Docker Hub publishing pipeline before the template string is finalized in Phase 1.

---

## Sources

### Primary (HIGH confidence)
- [Install Docker Engine on Debian](https://docs.docker.com/engine/install/debian/) — apt commands, arm64 support, Raspberry Pi OS path
- [Install Docker Engine on Ubuntu](https://docs.docker.com/engine/install/ubuntu/) — apt GPG key setup, repository setup, package names
- [Install Docker Engine on Fedora](https://docs.docker.com/engine/install/fedora/) — dnf config-manager commands
- [Install Docker Engine on RHEL](https://docs.docker.com/engine/install/rhel/) — RHEL-specific dnf repo URL
- [Docker Engine Linux post-install](https://docs.docker.com/engine/install/linux-postinstall/) — docker group usermod, newgrp behaviour, security warning
- [Docker Desktop WSL2 integration](https://docs.docker.com/desktop/features/wsl/) — socket bridge mechanism, WSL volume performance
- [Docker Compose Variable Interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/) — `${VAR}` syntax, `.env` quoting, `$$` escaping
- [Docker Compose startup ordering](https://docs.docker.com/compose/how-tos/startup-order/) — `depends_on` with `condition: service_healthy`
- [Docker Compose V2 GA announcement](https://www.docker.com/blog/announcing-compose-v2-general-availability/) — V1 EOL date December 2023
- [Python secrets module](https://docs.python.org/3/library/secrets.html) — `token_hex`, cryptographic security rationale
- [Python argparse documentation](https://docs.python.org/3/library/argparse.html) — subparsers, `set_defaults` dispatch pattern
- [Python subprocess documentation](https://docs.python.org/3/library/subprocess.html) — `check=True`, `capture_output`, list arguments
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/) — `~/.config` convention
- [readline ANSI prompt bug — CPython issue 17337](https://bugs.python.org/issue17337) — ANSI codes in `input()` prompt break readline cursor position

### Secondary (MEDIUM confidence)
- [ArchWiki Docker](https://wiki.archlinux.org/title/Docker) — pacman package name, systemctl required on Arch
- [WSL2 detection via /proc — scivision.dev](https://www.scivision.dev/python-detect-wsl/) — `/proc/sys/kernel/osrelease` WSL2 check pattern
- [Coolify Installation Docs](https://coolify.io/docs/get-started/installation) — reference installer comparison
- [Compose .env special characters issue](https://github.com/docker/compose/issues/5980) — `$` interpolation in password values confirmed
- [Compose tilde path issue](https://github.com/docker/compose/issues/6506) — `~` not expanded by Compose confirmed

### Tertiary (LOW confidence)
- None — all findings have at least MEDIUM confidence backing.

---
*Research completed: 2026-03-26*
*Ready for roadmap: yes*

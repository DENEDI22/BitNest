# Phase 10: Linux x86_64 Installer — Research

**Researched:** 2026-03-26
**Domain:** Python installer script — Textual TUI, Docker Engine auto-install, Docker Compose orchestration, stdlib subprocess patterns
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Script always opens a main menu (Textual app) with three options: Install BitNest / Update BitNest / Uninstall BitNest. User navigates with ↑↓, selects with Enter.
- **D-02:** On startup, check `~/.config/bitnest/install.json`. If it exists (prior install detected), skip to main menu with "Update BitNest" pre-highlighted. If it does not exist, pre-highlight "Install BitNest".
- **D-03:** Install wizard is 4 steps with a persistent step indicator at top: Step 1/4 Prerequisites → Step 2/4 Configuration → Step 3/4 Installing → Step 4/4 Done.
- **D-04:** Back/Next navigation between steps. User can go Back from Step 2; Steps 3 and 4 are non-interactive (progress + result only).
- **D-05:** Check and display status for: Docker Engine, Docker Compose V2 plugin, port availability (5000 and 3000 by default), disk space (warn if <5 GB free). Each check shows ✔ (pass) or ✗ (fail/action needed).
- **D-06:** If Docker is missing, show "Docker will be installed automatically" with ✗ replaced by a pending indicator. Actual Docker install happens when user advances to Step 3 (not during Step 1 check).
- **D-07:** Port conflicts detected in Step 1 are surfaced as ✗ with a note. User can change ports in Step 2 — re-validation happens on Next from Step 2.
- **D-08:** Prompts in Step 2 (in order): install directory, API port, frontend port, admin username (min 3 chars), admin password (min 8 chars, masked input).
- **D-09:** DB password and JWT signing key are auto-generated silently using `secrets.token_hex(32)`. NOT shown to user during wizard (they are in the `.env` file).
- **D-10:** Admin credentials (username + password) are written to `.env` as `BITNEST_ADMIN_USER` and `BITNEST_ADMIN_PASS`. The API must consume these env vars to seed the admin account on first startup if no users exist yet.
- **D-11:** Step 3 sequence (all shown with live status lines): Install Docker if missing → Add user to docker group + use sudo docker for remaining calls → Create install dir and subdirs → Write compose.yaml → Write .env (chmod 600) → docker compose pull with animated spinner → docker compose up -d → Health poll loop (60s timeout).
- **D-12:** Success screen shows: ✔ All services healthy, Frontend URL, API URL, "Your admin account has been created", admin username (not password), re-run hint.
- **D-13:** Docker Hub username is `denedi22`. Images: `denedi22/bitnest_api:latest`, `denedi22/bitnest_frontend:latest`, `postgres:16`.
- **D-14:** Always pull `:latest`. No version pinning in v0.1.0.
- **D-15:** compose.yaml embedded as Python string constant using `str.format()`. Python variables use `{python_var}`, Docker variables use `${{DATA_DIR}}` in Python source (produces `${DATA_DIR}` in output).
- **D-16:** Bind mounts: `{install_dir}/data/storage` → `/app/data/storage` (API files), `{install_dir}/data/postgres` → `/var/lib/postgresql/data` (DB).
- **D-17:** pg_isready healthcheck on db service and `condition: service_healthy` on api's depends_on. MANDATORY in template.
- **D-18:** All services have `restart: unless-stopped`.
- **D-19:** Admin seed env vars in compose template: `BITNEST_ADMIN_USER: ${BITNEST_ADMIN_USER}` and `BITNEST_ADMIN_PASS: ${BITNEST_ADMIN_PASS}` on the api service.
- **D-20:** State file at `~/.config/bitnest/install.json` after successful install. Format: `{ "install_dir", "api_port", "frontend_port", "compose_file", "installed_at" }`. Secrets NOT stored.
- **D-21:** Update flow: read state file → docker compose pull → docker compose up -d → health poll → show result. No `docker compose down`.
- **D-22:** Uninstall flow: read state file → confirm screen → `docker compose down` → second confirm (red warning) → if yes: shutil.rmtree(install_dir) → delete state file.
- **D-23:** Use `secrets.token_hex(32)` for DB password and JWT signing key.
- **D-24:** All docker compose calls use `subprocess.run(["docker", "compose", "-f", abs_compose_path, ...], check=True)`. Never use `cwd=`. Never use `shell=True`. Sudo only for Docker install and usermod steps.

### Claude's Discretion

- Exact Textual widget types (Input, Button, Label, ProgressBar, etc.) and layout composition
- Exact ANSI color scheme / Textual CSS
- Spinner animation implementation during docker pull
- Exact distro detection logic (reading `/etc/os-release` ID and ID_LIKE fields)
- Health poll implementation details (polling interval, per-service status parsing)

### Deferred Ideas (OUT OF SCOPE)

- Version pinning (`--version` flag) — deferred to v2
- `--non-interactive` / `--yes` flags — Phase 13
- PyInstaller bundling and build spec — Phase 13
- GitHub Actions release pipeline — Phase 13
- ARM64-specific Docker repo config (`arch=arm64`) — Phase 11
- WSL2 Docker Desktop guidance — Phase 12
- Backup `.env` before update — v2 requirement
- Systemd service unit for auto-start — v2 requirement
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INST-01 | User launches a Textual TUI that walks through installation step-by-step with Back/Next navigation | Textual Screen stack: push_screen() to advance, pop_screen() to go Back. Each step = one Screen subclass. |
| INST-02 | TUI prompts for install directory, API port, and frontend port with inline validation | Textual Input widget with validators parameter; re-validate on Next button press event. |
| INST-03 | Installer auto-generates a cryptographically secure DB password and JWT signing key | `secrets.token_hex(32)` — hex-only output, no .env interpolation hazards. |
| INST-04 | Installer runs prerequisite checks (Docker, Compose V2, port availability, disk space) before wizard begins | `shutil.which("docker")`, `subprocess.run(["docker", "compose", "version"])`, `socket.bind()` port check, `shutil.disk_usage("/")`. |
| INST-05 | Installer creates install directory with `data/storage/` and `data/postgres/` subdirectories | `pathlib.Path.mkdir(parents=True, exist_ok=True)` — idempotent; run as current user. |
| INST-06 | Installer writes `.env` (chmod 600) and `compose.yaml` with bind mounts and `pg_isready` healthcheck | `Path.write_text()` + `os.chmod(path, 0o600)`; compose template as `str.format()` string constant with `${{VAR}}` for Docker vars. |
| INST-07 | Installer shows a live progress screen with output while pulling Docker Hub images | `subprocess.Popen` with stdout pipe in a Textual `@work` thread worker; `RichLog.write(line)` per line for real-time display. |
| INST-08 | Installer starts the stack with DB-before-API ordering via `condition: service_healthy` | Embedded in compose template: db service has `pg_isready` healthcheck; api has `depends_on: db: condition: service_healthy`. |
| INST-09 | Installer polls per-service health and shows pass/fail status before declaring success | `subprocess.run(["docker", "compose", "-f", ..., "ps", "--format", "json"])` in a loop; parse JSON per-service `Health` field. |
| INST-10 | Installer saves install state to `~/.config/bitnest/install.json` | `json.dumps()` to `Path(os.environ.get("XDG_CONFIG_HOME") or Path.home()/".config")/"bitnest"/"install.json"`. |
| INST-11 | User can launch the update flow via TUI to pull latest images and rolling-restart the stack | Update flow from main menu: read state → compose pull → compose up -d → health poll. No compose down. |
| INST-12 | User can launch the uninstall flow via TUI with explicit confirmation before any data is deleted | Two-screen confirmation pattern in Textual; `shutil.rmtree(install_dir)`; state file deleted last. |
| LINUX-01 | Installer detects missing Docker Engine and installs it automatically (apt/dnf/pacman with get.docker.com fallback) | Read `/etc/os-release` ID and ID_LIKE fields; dispatch to apt/dnf/pacman/fallback; exact install commands per distro documented in STACK.md. |
| LINUX-02 | Installer escalates to sudo only for Docker install steps; all other operations run as current user | Use `["sudo", "apt", ...]` / `["sudo", "dnf", ...]` for install only; use `["sudo", "docker", "compose", ...]` for the post-usermod compose calls. |
</phase_requirements>

---

## Summary

Phase 10 delivers `installers/linux-x86_64/install.py` — a single Python file that takes a user from a bare Linux x86_64 machine to a running BitNest Docker Compose stack. The CONTEXT.md locked in Textual as the TUI framework (D-01 through D-04), which supersedes the earlier "stdlib-only" constraint from the project research phase. Textual must be installed as a prerequisite, either pre-checked with a clear error or auto-installed via pip before the TUI launches. Everything else in the installer (subprocess calls, file I/O, JSON state, secrets) uses only stdlib.

The architecture is a single self-contained Python file with a Textual App hosting four step-based Screen subclasses (Prerequisites, Configuration, Installing, Done) and a Main Menu screen. Screen navigation uses `push_screen()` to advance and `pop_screen()` to go Back. The Step 3 (Installing) screen runs all Docker orchestration in a Textual `@work` thread worker, streaming `subprocess.Popen` stdout to a `RichLog` widget line-by-line. The compose.yaml is embedded as a Python triple-quoted string constant using `str.format()` substitution — `{python_var}` for installer-time values, `${{VAR}}` in Python source to produce Docker-native `${VAR}` in output.

This phase freezes all shared patterns (Textual screen structure, compose template, state schema, subprocess wrapper) before Phase 11 (ARM64) copies from this baseline. The API must also be extended to read `BITNEST_ADMIN_USER`/`BITNEST_ADMIN_PASS` env vars on startup and seed an admin account if no users exist — this is a small addition to the existing auth system and is a blocking integration point.

**Primary recommendation:** Build the installer as one Textual App with five Screen subclasses (MainMenu, Step1Prerequisites, Step2Configuration, Step3Installing, Step4Done); use thread workers for all blocking subprocess calls; never `shell=True`; never `cwd=`; always expand user paths before writing to compose.yaml or .env.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Python | 3.8+ | Installer runtime | 3.8 minimum: f-strings, `secrets`, `pathlib`, `subprocess.run` all available; present on Debian Buster and later |
| textual | 8.1.1 (current) | Full-screen TUI framework | Locked by D-01; provides Screen, Input, Button, RichLog, Label, ListView, ProgressBar widgets |
| subprocess (stdlib) | built-in | Run docker, apt/dnf/pacman, usermod | `subprocess.run(..., check=True)` with list args; `Popen` for streaming output |
| secrets (stdlib) | Python 3.6+ | Generate DB password and JWT key | `secrets.token_hex(32)` — cryptographically secure, hex-only (no .env interpolation hazards) |
| pathlib (stdlib) | Python 3.4+ | Path creation, expansion, resolution | `Path.mkdir(parents=True, exist_ok=True)` is idempotent; `Path.expanduser().resolve()` for user paths |
| json (stdlib) | built-in | State file read/write | `json.dumps()` / `json.loads()` for `~/.config/bitnest/install.json` |
| shutil (stdlib) | built-in | `shutil.which()` for binary detection; `shutil.rmtree()` for uninstall; `shutil.disk_usage()` for disk check | More portable than hardcoded paths |
| socket (stdlib) | built-in | Port availability pre-flight check | `socket.bind(("0.0.0.0", port))` — fails if port is already bound |
| os (stdlib) | built-in | `os.chmod()` for .env permissions; `os.getuid()` for root check; `os.environ` | `os.chmod(path, 0o600)` immediately after .env write |
| platform (stdlib) | built-in | `platform.machine()` for CPU arch detection | Returns `x86_64` on this platform; used for conditional logic |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| getpass (stdlib) | built-in | Admin password masked input fallback | If Textual Input(password=True) is not sufficient; also used for stdlib-only fallback |
| time (stdlib) | built-in | `time.sleep()` in health poll loop | Brief sleep between `docker compose ps` poll iterations |
| threading (stdlib) | built-in | Spinner on non-Textual blocking calls | For any blocking call outside Textual's `@work` worker pattern |
| configparser (stdlib) | built-in | Alternative /etc/os-release parser | Can parse as INI; simpler than manual line splitting for distro detection |
| re (stdlib) | built-in | Input validation (port range, path format) | `re.match(r'^\d{1,5}$', val)` for ports |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Textual (8.1.1) | urwid, curses (stdlib) | Textual locked by D-01; urwid has less polish; curses requires deep terminal knowledge |
| `str.format()` for compose template | `string.Template` | `string.Template` treats `$` as its own delimiter — conflicts with Docker's `${VAR}` syntax. `str.format()` is the only safe choice. |
| `subprocess.Popen` in @work thread | `asyncio.create_subprocess_exec` | Textual is asyncio-based; both work; `@work` with `Popen` is the community-recommended pattern for streaming subprocess output to RichLog |
| `secrets.token_hex(32)` | `os.urandom(32).hex()` | Functionally equivalent; `secrets` is the documented stdlib API for security-sensitive token generation |

**Installation (development):**
```bash
pip install textual==8.1.1
```

**Installation (end-user, from within installer script):**
```python
import subprocess, sys
try:
    import textual
except ImportError:
    print("Installing required dependency (textual)...")
    subprocess.run([sys.executable, "-m", "pip", "install", "textual>=0.89.0"], check=True)
    import textual
```

**Version verification:** Confirmed 2026-03-26 via `pip3 index versions textual` — current latest is 8.1.1.

---

## Architecture Patterns

### Recommended Project Structure

```
installers/
├── linux-x86_64/
│   └── install.py        # this phase — complete self-contained installer
├── linux-arm64/          # Phase 11 — copy + ARM diff
└── windows-wsl2/         # Phase 12 — copy + WSL2 diff
```

Each file is completely self-contained: one file download, one `python3 install.py` invocation, no imports beyond stdlib + textual.

### Pattern 1: Textual App with Screen Stack (Wizard Navigation)

**What:** One `App` subclass hosts five `Screen` subclasses. Navigation uses `push_screen()` to advance and `pop_screen()` to go Back. Steps 3 and 4 are non-interactive — no Back button.

**When to use:** Any multi-step wizard with linear Back/Next navigation.

```python
# Source: https://textual.textualize.io/guide/screens/
from textual.app import App, ComposeResult
from textual.screen import Screen
from textual.widgets import Button, Label, ListView, ListItem, Input, RichLog

class MainMenuScreen(Screen):
    def compose(self) -> ComposeResult:
        yield Label("BitNest Installer", id="title")
        yield ListView(
            ListItem(Label("Install BitNest"), id="install"),
            ListItem(Label("Update BitNest"), id="update"),
            ListItem(Label("Uninstall BitNest"), id="uninstall"),
        )

    def on_list_view_selected(self, event: ListView.Selected) -> None:
        if event.item.id == "install":
            self.app.push_screen(Step1PrerequisitesScreen())
        elif event.item.id == "update":
            self.app.push_screen(UpdateScreen())
        elif event.item.id == "uninstall":
            self.app.push_screen(UninstallConfirmScreen())

class Step2ConfigurationScreen(Screen):
    def compose(self) -> ComposeResult:
        yield Label("Step 2/4 Configuration")
        yield Input(placeholder="Install directory (default: ~/bitnest)", id="install_dir")
        yield Input(placeholder="API port (default: 5000)", id="api_port")
        yield Input(placeholder="Frontend port (default: 3000)", id="frontend_port")
        yield Input(placeholder="Admin username (min 3 chars)", id="admin_user")
        yield Input(placeholder="Admin password (min 8 chars)", password=True, id="admin_pass")
        yield Button("Back", id="back")
        yield Button("Next", id="next")

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "back":
            self.app.pop_screen()
        elif event.button.id == "next":
            if self._validate():
                self.app.push_screen(Step3InstallingScreen(self._collect_config()))

class InstallerApp(App):
    def on_mount(self) -> None:
        state = read_state()
        screen = MainMenuScreen()
        # Pre-highlight based on state file existence (D-02)
        self.push_screen(screen)
```

### Pattern 2: Streaming Subprocess Output to RichLog

**What:** Run `docker compose pull` in a Textual `@work` thread worker using `subprocess.Popen`. Write each stdout line to a `RichLog` widget as it arrives.

**When to use:** Any long-running subprocess call that must stream output to the TUI (docker pull, docker up).

```python
# Source: https://github.com/Textualize/textual/discussions/3788
from textual import work
from textual.widgets import RichLog
from rich.text import Text
import subprocess

class Step3InstallingScreen(Screen):

    @work(exclusive=True)
    async def run_install(self) -> None:
        log = self.query_one(RichLog)
        compose_file = str(self.cfg["compose_file"])
        cmd = ["sudo", "docker", "compose", "-f", compose_file, "pull"]
        with subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True
        ) as proc:
            for line in proc.stdout:
                # Convert ANSI codes from docker pull output
                log.write(Text.from_ansi(line.rstrip()))
```

### Pattern 3: compose.yaml Template via str.format()

**What:** The compose.yaml is embedded as a Python triple-quoted string constant. Python-time variables use `{python_var}`. Docker Compose runtime variables use `${{VAR}}` in Python source (produces `${VAR}` in the output file).

**When to use:** Always — do NOT use `string.Template` (treats `$` as its own delimiter, conflicts with Docker syntax).

```python
# Source: https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/
COMPOSE_TEMPLATE = """\
services:
  api:
    image: {docker_hub_user}/bitnest_api:latest
    container_name: bitnest_api
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    env_file:
      - .env
    environment:
      BITNEST_ADMIN_USER: ${{BITNEST_ADMIN_USER}}
      BITNEST_ADMIN_PASS: ${{BITNEST_ADMIN_PASS}}
      ASPNETCORE_ENVIRONMENT: "Production"
      ConnectionStrings__DefaultConnection: "Host=db;Port=5432;Database=bitnest;Username=bitnest;Password=${{POSTGRES_PASSWORD}}"
      UploadsPath: "/app/data/storage"
      Auth__SigningKey: ${{AUTH_SIGNING_KEY}}
    ports:
      - "{api_port}:8080"
    volumes:
      - ${{DATA_DIR}}/data/storage:/app/data/storage

  db:
    image: postgres:16
    container_name: bitnest_db
    restart: unless-stopped
    env_file:
      - .env
    environment:
      POSTGRES_DB: bitnest
      POSTGRES_USER: bitnest
      POSTGRES_PASSWORD: ${{POSTGRES_PASSWORD}}
    volumes:
      - ${{DATA_DIR}}/data/postgres:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U bitnest -d bitnest"]
      interval: 5s
      timeout: 3s
      retries: 10
      start_period: 10s

  frontend:
    image: {docker_hub_user}/bitnest_frontend:latest
    container_name: bitnest_frontend
    restart: unless-stopped
    ports:
      - "{frontend_port}:80"
"""
# Usage: COMPOSE_TEMPLATE.format(docker_hub_user="denedi22", api_port=5000, frontend_port=3000)
# Output: valid compose.yaml with ${DATA_DIR}, ${POSTGRES_PASSWORD}, ${AUTH_SIGNING_KEY}
# intact for Docker Compose to resolve from .env at runtime
```

### Pattern 4: .env Writer (chmod 600 immediately)

**What:** Write all runtime configuration to .env adjacent to compose.yaml. chmod 600 immediately after write. Expand all paths to absolute before writing.

```python
import os
from pathlib import Path

def write_env_file(env_path: Path, cfg: dict) -> None:
    lines = [
        "# BitNest configuration — generated by installer",
        f"DATA_DIR={cfg['install_dir']}",   # absolute path — no ~ allowed
        f"POSTGRES_PASSWORD={cfg['db_password']}",
        f"AUTH_SIGNING_KEY={cfg['jwt_key']}",
        f"BITNEST_ADMIN_USER={cfg['admin_user']}",
        f"BITNEST_ADMIN_PASS={cfg['admin_pass']}",
    ]
    env_path.write_text("\n".join(lines) + "\n")
    os.chmod(env_path, 0o600)  # MUST follow immediately after write
```

### Pattern 5: Port Pre-flight Check

**What:** Check all ports the stack will bind BEFORE the wizard begins. Re-check after Step 2 if user changes ports.

```python
# Source: https://docs.python.org/3/library/socket.html
import socket

def is_port_free(port: int) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 0)
        try:
            s.bind(("0.0.0.0", port))
            return True
        except OSError:
            return False
```

### Pattern 6: Distro Detection for Docker Install

**What:** Read `/etc/os-release` to select the correct Docker install path. Check `ID` first, then `ID_LIKE` for derivatives.

```python
# Source: https://docs.docker.com/engine/install/debian/ and https://docs.docker.com/engine/install/fedora/
def read_os_release() -> dict:
    result = {}
    try:
        for line in Path("/etc/os-release").read_text().splitlines():
            line = line.strip()
            if "=" in line and not line.startswith("#"):
                k, _, v = line.partition("=")
                result[k] = v.strip('"').strip("'")
    except OSError:
        pass
    return result

def get_docker_install_path(info: dict) -> str:
    distro_id  = info.get("ID", "").lower()
    id_like    = info.get("ID_LIKE", "").lower()
    if distro_id == "arch":
        return "pacman"
    if "ubuntu" in distro_id or "ubuntu" in id_like:
        return "apt_ubuntu"
    if distro_id in ("debian", "raspbian") or "debian" in id_like:
        return "apt_debian"
    if distro_id == "fedora":
        return "dnf_fedora"
    if distro_id in ("rhel", "centos", "rocky", "almalinux") or "rhel" in id_like:
        return "dnf_rhel"
    return "fallback"   # get.docker.com convenience script
```

### Pattern 7: State File Read/Write

**What:** Write state JSON to XDG config dir after successful install. Read on startup to detect prior install. Delete last during uninstall.

```python
import json, os
from pathlib import Path
from datetime import timezone, datetime

def state_path() -> Path:
    base = Path(os.environ.get("XDG_CONFIG_HOME") or (Path.home() / ".config"))
    return base / "bitnest" / "install.json"

def write_state(install_dir: str, api_port: int, frontend_port: int, compose_file: str) -> None:
    p = state_path()
    p.parent.mkdir(parents=True, exist_ok=True)
    data = {
        "install_dir": install_dir,
        "api_port": api_port,
        "frontend_port": frontend_port,
        "compose_file": compose_file,
        "installed_at": datetime.now(timezone.utc).isoformat(),
    }
    p.write_text(json.dumps(data, indent=2))

def read_state() -> dict | None:
    p = state_path()
    return json.loads(p.read_text()) if p.exists() else None
```

### Anti-Patterns to Avoid

- **`string.Template` for compose.yaml:** Treats `$` as its own placeholder — conflicts with Docker's `${VAR}` syntax. Use `str.format()` exclusively.
- **`shell=True` in subprocess:** Enables shell injection via user-provided directory names or ports. Always pass commands as lists.
- **`cwd=install_dir` instead of `-f`:** Docker Compose resolves .env relative to the `-f` compose file path, not the process cwd. Use `-f <abs_path>` always.
- **`newgrp docker` via subprocess:** Opens an interactive subshell that blocks the installer. Use `sudo docker compose` for the current session instead.
- **Tilde or relative paths in compose.yaml:** Docker Compose does not expand `~`. Always call `Path(user_input).expanduser().resolve()` before writing to any file.
- **`docker compose down` in update flow:** Removes containers and can drop volumes. Use `docker compose up -d` with already-pulled images only.
- **`random` module for secrets:** Not cryptographically secure. Use `secrets.token_hex(32)` exclusively.
- **State file inside install_dir:** Uninstall deletes install_dir; if state is inside it, cleanup fails. State always at `~/.config/bitnest/install.json`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Full-screen TUI with keyboard nav | Custom curses/ANSI state machine | `textual` 8.1.1 | Textual handles terminal resize, keyboard routing, focus management, widget layout — all are deceptively complex to hand-roll |
| Masked password input in TUI | Custom ANSI character-hiding loop | `textual.widgets.Input(password=True)` | Textual handles this correctly; custom readline masking has known cursor-position bugs |
| Live-streaming subprocess output | Custom threading + ANSI print loop | `@work` worker + `subprocess.Popen` + `RichLog.write()` | Textual's worker pattern handles thread-to-UI message passing correctly; bare threads require `call_from_thread()` |
| Input validation in TUI | Manual if/elif checks on each field | `textual.validation.Number`, `textual.validation.Length`, custom `Validator` subclass | Textual validators integrate with Input's changed/submitted events and provide inline error messaging |
| Docker Engine installation | Custom script per distro | Official distro-specific commands (apt/dnf/pacman) from Docker docs | Edge cases: GPG key trust, apt source format, package names differ subtly by distro version |

**Key insight:** The TUI layer (Textual) is the only external dependency. Every other piece (subprocess, secrets, pathlib, json, socket) is stdlib. This keeps the "one pip install, one script" model intact.

---

## Critical Integration Point: API Admin Seeding

The CONTEXT.md (D-10) requires the API to read `BITNEST_ADMIN_USER` and `BITNEST_ADMIN_PASS` env vars on startup and create an admin account if no users exist. The existing API (Phase 6/7 work) does not currently do this. This is a blocking integration point — the installer writes these vars into the compose template, but they are useless if the API ignores them.

**What the API needs:**
- On startup: if `BITNEST_ADMIN_USER` and `BITNEST_ADMIN_PASS` env vars are present AND the users table is empty, create a user with admin role using the existing user-creation code path.
- The API auth config key is `Auth__SigningKey` (from `appsettings.json`) — the installer must write this value to `.env` as `Auth__SigningKey` (double-underscore ASP.NET Core convention for nested config).
- Connection string format confirmed from `appsettings.json`: `Host=db;Port=5432;Database=bitnest;Username=bitnest;Password=<pw>`

**Confirmed env var names from appsettings.json:**
- `ConnectionStrings__DefaultConnection` — full Npgsql connection string
- `Auth__SigningKey` — JWT signing key
- `UploadsPath` — file storage path (hardcode `/app/data/storage` in compose template)
- `ASPNETCORE_ENVIRONMENT` — hardcode `Production`

---

## Docker Compose Template Complete Reference

Confirmed service names, ports, and volumes from `compose.yaml`:
- `api` service: internal port 8080, external configurable (default 5000), volume `/app/data/storage`
- `db` service: postgres:16, internal port 5432, volume `/var/lib/postgresql/data`, DB name `bitnest`, user `bitnest`
- `frontend` service: internal port 80, external configurable (default 3000)

Docker Hub image tags confirmed from `.github/workflows/docker-image.yml`:
- `${{ secrets.DOCKERHUB_USERNAME }}/bitnest_api:latest` — hardcoded as `denedi22/bitnest_api:latest` per D-13
- `${{ secrets.DOCKERHUB_USERNAME }}/bitnest_frontend:latest` — hardcoded as `denedi22/bitnest_frontend:latest` per D-13
- Multi-arch build confirmed: `platforms: linux/amd64,linux/arm64` — ARM64 images ARE published

---

## Common Pitfalls

### Pitfall 1: Docker Group Not Active in Same Session
**What goes wrong:** After `sudo usermod -aG docker $USER`, the running Python process still has the old group credentials. The next `docker compose` call gets "permission denied" on the Docker socket.
**Why it happens:** UNIX group memberships are embedded in the process credential set at login. `usermod` updates `/etc/group` but the running process does not re-read it.
**How to avoid:** Per D-11 and PITFALLS.md, use `sudo docker compose ...` for ALL remaining compose calls in the install session. Tell user to log out and back in for future sessions without sudo.
**Warning signs:** `permission denied while trying to connect to the Docker daemon socket` immediately after the Docker install step.

### Pitfall 2: Textual Requires pip — Not Available on All Machines
**What goes wrong:** User runs `python3 install.py` on a bare machine without Textual installed. ImportError before anything is shown.
**Why it happens:** Textual is not stdlib. The project research phase said "stdlib only" but the CONTEXT.md locked in Textual, creating a dependency.
**How to avoid:** At the top of `install.py`, before importing textual, try to import it; if ImportError, use `subprocess.run([sys.executable, "-m", "pip", "install", "textual>=0.89.0"])`. Print a simple text-mode "Installing dependencies..." message before the TUI launches.
**Warning signs:** `ModuleNotFoundError: No module named 'textual'` on first run.

### Pitfall 3: special characters in .env Breaking Interpolation
**What goes wrong:** Docker Compose performs shell-style `$` interpolation on `.env` values. Passwords with `$` characters get mangled.
**How to avoid:** Use `secrets.token_hex(32)` exclusively — produces only `[0-9a-f]`. Never use `random`, never use `token_urlsafe` (contains `-` and `_` which are safe, but `token_hex` is cleaner).

### Pitfall 4: Tilde Paths in compose.yaml Volume Mounts
**What goes wrong:** `~/bitnest/data/storage:/app/data/storage` — Docker Compose does not expand `~`. Container runtime rejects it or creates a directory literally named `~`.
**How to avoid:** Always `Path(user_input).expanduser().resolve()` before writing. Every path in compose.yaml or .env must be an absolute path.

### Pitfall 5: Container Startup Race (API before DB Ready)
**What goes wrong:** `docker compose up -d` starts API before PostgreSQL accepts connections. API crashes with connection refused, restarts, eventually stabilizes but user sees errors.
**How to avoid:** Mandatory in the compose template (D-17): `pg_isready` healthcheck on db service + `condition: service_healthy` on api's `depends_on`.

### Pitfall 6: Port Conflict Discovered After Config Wizard
**What goes wrong:** Docker gives a cryptic "bind: address already in use" error after a multi-step wizard. User must restart entire flow.
**How to avoid:** Run port checks (Step 1, D-05) before the wizard. Surface as ✗ in Step 1; user can change ports in Step 2; re-validate on "Next" from Step 2 (D-07).

### Pitfall 7: compose.yaml Has top-level `version:` Key
**What goes wrong:** Docker Compose V2 emits a deprecation warning for `version:` at top level; V3 ignores it silently.
**How to avoid:** Do not include `version:` in the compose template at all. It is obsolete in Compose V2.

### Pitfall 8: Installer File/Directory Owned by Root
**What goes wrong:** User runs `sudo python3 install.py`. All created files are `root:root`. PostgreSQL data dir refuses to accept `chmod 700` from the `postgres` container user (UID 999).
**How to avoid:** Design installer to run as normal user; use `["sudo", "apt", ...]` etc. for escalated steps only. If running as root is detected (`os.getuid() == 0`), look up `SUDO_USER` and use `os.chown()` to fix ownership.

### Pitfall 9: Textual @work Worker Blocking the Event Loop
**What goes wrong:** Long-running code in a Textual `async def on_button_pressed` handler blocks the event loop — UI freezes during docker pull.
**How to avoid:** All blocking subprocess calls go in a `@work(exclusive=True)` decorated async method. Textual runs this in a thread worker, keeping the event loop free.

---

## Code Examples

### Health Poll Loop

```python
# Source: https://docs.docker.com/reference/cli/docker/compose/ps/
import subprocess, json, time
from pathlib import Path

def poll_health(compose_file: str, timeout: int = 60) -> dict[str, bool]:
    """Poll docker compose ps until all services are healthy or timeout."""
    deadline = time.time() + timeout
    services = {"api", "db", "frontend"}
    while time.time() < deadline:
        result = subprocess.run(
            ["docker", "compose", "-f", compose_file, "ps", "--format", "json"],
            capture_output=True, text=True
        )
        statuses = {}
        for line in result.stdout.strip().splitlines():
            try:
                svc = json.loads(line)
                name = svc.get("Service", "")
                health = svc.get("Health", "")
                state = svc.get("State", "")
                statuses[name] = (health == "healthy") or (state == "running" and health == "")
            except json.JSONDecodeError:
                pass
        if all(statuses.get(s) for s in services):
            return {s: True for s in services}
        time.sleep(3)
    # Timeout: return what we have
    return statuses
```

### Docker Engine Presence Check

```python
import shutil, subprocess

def check_docker() -> tuple[bool, bool]:
    """Returns (docker_installed, compose_v2_available)."""
    docker_installed = shutil.which("docker") is not None
    if not docker_installed:
        return False, False
    r = subprocess.run(
        ["docker", "compose", "version"],
        capture_output=True, text=True, timeout=10
    )
    return True, r.returncode == 0
```

### Disk Space Check

```python
import shutil

def check_disk_space(path: str = "/", min_gb: float = 5.0) -> tuple[bool, float]:
    """Returns (has_enough, free_gb)."""
    usage = shutil.disk_usage(path)
    free_gb = usage.free / (1024 ** 3)
    return free_gb >= min_gb, round(free_gb, 1)
```

### Admin Password Textual Input (Masked)

```python
# Source: https://textual.textualize.io/widgets/input/
from textual.widgets import Input
from textual.validation import Length

admin_pass_input = Input(
    placeholder="Admin password (min 8 characters)",
    password=True,
    id="admin_pass",
    validators=[Length(minimum=8)],
    validate_on=["submitted"]
)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `docker-compose` (hyphen, V1 standalone) | `docker compose` (space, V2 plugin) | EOL December 2023 | V1 absent from Docker repos; detection must check V2 form first |
| `version:` top-level key in compose.yaml | Omit entirely | Compose V2 | Deprecation warning if present; clean files omit it |
| Named Docker volumes for user data | Bind mounts to user-chosen directory | Decision in CONTEXT.md | Users can find, back up, and migrate data directly |
| `argparse` subcommands (earlier research) | Textual TUI with screen-based flows | CONTEXT.md D-01 to D-04 | Textual locks in a richer UX; requires pip install but improves all user interaction |

**Deprecated/outdated:**
- `docker-compose` (hyphen): EOL December 2023; no longer in Docker's official repos. Never use as primary command.
- `version:` in compose.yaml: Obsolete in Compose V2. Do not include in template.
- `string.Template` for Docker compose generation: Conflicts with Docker's `${VAR}` syntax. Use `str.format()`.

---

## Open Questions

1. **Textual vs. stdlib-only conflict**
   - What we know: STATE.md says "stdlib only — no pip install required from end-users" but CONTEXT.md D-01 locked in Textual.
   - What's unclear: Whether the installer should auto-install Textual silently (via pip at startup), require it as a pre-condition with a clear error, or whether the "stdlib only" constraint was superseded by the Textual decision.
   - Recommendation: Auto-install Textual via `subprocess.run([sys.executable, "-m", "pip", "--quiet", "install", "textual"])` at the top of the script before the TUI launches. Print a simple text-mode "Installing UI dependency..." line. This keeps the single-file, single-invocation UX while honoring the Textual TUI decision.

2. **API admin seeding — scope of API change**
   - What we know: D-10 requires API to read `BITNEST_ADMIN_USER`/`BITNEST_ADMIN_PASS` on startup and create admin if users table is empty. This was not built in Phase 6/7.
   - What's unclear: Whether this is in-scope for Phase 10 or a pre-existing gap that must be resolved before Phase 10 can be declared complete.
   - Recommendation: Include a Wave 0 task in Phase 10 to add admin seeding to the API startup code. It is a small addition to the existing user-creation flow (check `context.Users.Any()`, if false create admin from env vars). Without this, the installer produces a stack with no admin account.

3. **`docker compose ps --format json` output format**
   - What we know: The `--format json` flag outputs one JSON object per line in Compose V2. The `Health` field is only populated for services with healthchecks; the `db` service will show `healthy` after the pg_isready check; `api` and `frontend` will show `running` with empty `Health`.
   - What's unclear: Exact field names across all Docker Compose V2 minor versions (5.x releases as seen on this machine).
   - Recommendation: Parse both `Health == "healthy"` and `State == "running"` as passing — services without a healthcheck show `running` not `healthy`.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Python 3 | Installer runtime | ✓ | 3.14.3 (dev machine); 3.8+ on target) | — |
| pip | Textual installation | ✓ | 26.0.1 (dev); standard on most distros | `ensurepip` module if pip absent |
| Docker Engine | Stack orchestration | ✓ | 28.5.2 (dev machine) | Auto-install via LINUX-01 |
| Docker Compose V2 plugin | All compose commands | ✓ | 5.1.1 (dev machine) | Installed as part of docker-compose-plugin package |
| textual | TUI framework | Not pre-installed | 8.1.1 (current) | Auto-install via pip at script start |
| pytest | Testing installer code | ✓ | 8.4.2 | — |

**Missing dependencies with no fallback:**
- None — all blocking dependencies have either auto-install paths or are present.

**Missing dependencies with fallback:**
- `textual`: auto-install via pip at script startup.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | pytest 8.4.2 |
| Config file | None — create `installers/linux-x86_64/pyproject.toml` in Wave 0 |
| Quick run command | `pytest installers/linux-x86_64/tests/ -x -q` |
| Full suite command | `pytest installers/linux-x86_64/tests/ -v` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| INST-02 | Input validation: port range 1-65535 | unit | `pytest installers/linux-x86_64/tests/test_validation.py::test_port_validation -x` | ❌ Wave 0 |
| INST-02 | Input validation: install dir expands tilde | unit | `pytest installers/linux-x86_64/tests/test_validation.py::test_path_expansion -x` | ❌ Wave 0 |
| INST-03 | secrets.token_hex(32) produces hex-only 64-char string | unit | `pytest installers/linux-x86_64/tests/test_secrets.py::test_token_is_hex -x` | ❌ Wave 0 |
| INST-04 | Port pre-flight: occupied port detected as not free | unit | `pytest installers/linux-x86_64/tests/test_preflight.py::test_port_occupied -x` | ❌ Wave 0 |
| INST-04 | Disk space check: returns correct free_gb and pass/fail | unit | `pytest installers/linux-x86_64/tests/test_preflight.py::test_disk_space -x` | ❌ Wave 0 |
| INST-05 | Install dir creates data/storage and data/postgres subdirs | unit | `pytest installers/linux-x86_64/tests/test_filesystem.py::test_create_install_dirs -x` | ❌ Wave 0 |
| INST-06 | .env written with chmod 600 | unit | `pytest installers/linux-x86_64/tests/test_filesystem.py::test_env_permissions -x` | ❌ Wave 0 |
| INST-06 | compose.yaml contains pg_isready healthcheck | unit | `pytest installers/linux-x86_64/tests/test_compose_template.py::test_healthcheck_present -x` | ❌ Wave 0 |
| INST-06 | compose.yaml: no tilde or relative paths | unit | `pytest installers/linux-x86_64/tests/test_compose_template.py::test_no_tilde_in_paths -x` | ❌ Wave 0 |
| INST-06 | compose.yaml: str.format() produces valid YAML with ${VAR} intact | unit | `pytest installers/linux-x86_64/tests/test_compose_template.py::test_format_substitution -x` | ❌ Wave 0 |
| INST-09 | Health poll returns per-service pass/fail dict | unit | `pytest installers/linux-x86_64/tests/test_health.py::test_poll_parsing -x` | ❌ Wave 0 |
| INST-10 | State file written and readable round-trip | unit | `pytest installers/linux-x86_64/tests/test_state.py::test_state_roundtrip -x` | ❌ Wave 0 |
| LINUX-01 | Distro detection: debian/ubuntu → apt, fedora → dnf, arch → pacman, unknown → fallback | unit | `pytest installers/linux-x86_64/tests/test_distro.py::test_distro_detection -x` | ❌ Wave 0 |

Note: INST-01 (Textual TUI launch), INST-07 (live streaming), INST-08 (service startup ordering), INST-11 (update flow), INST-12 (uninstall flow), and LINUX-02 (sudo escalation) are integration/e2e tests that require a Docker environment and are validated manually during acceptance testing, not via automated pytest.

### Sampling Rate

- **Per task commit:** `pytest installers/linux-x86_64/tests/ -x -q`
- **Per wave merge:** `pytest installers/linux-x86_64/tests/ -v`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `installers/linux-x86_64/` directory — does not exist yet
- [ ] `installers/linux-x86_64/install.py` — main deliverable
- [ ] `installers/linux-x86_64/tests/` directory
- [ ] `installers/linux-x86_64/tests/test_validation.py` — covers INST-02
- [ ] `installers/linux-x86_64/tests/test_secrets.py` — covers INST-03
- [ ] `installers/linux-x86_64/tests/test_preflight.py` — covers INST-04
- [ ] `installers/linux-x86_64/tests/test_filesystem.py` — covers INST-05, INST-06
- [ ] `installers/linux-x86_64/tests/test_compose_template.py` — covers INST-06
- [ ] `installers/linux-x86_64/tests/test_health.py` — covers INST-09
- [ ] `installers/linux-x86_64/tests/test_state.py` — covers INST-10
- [ ] `installers/linux-x86_64/tests/test_distro.py` — covers LINUX-01
- [ ] `installers/linux-x86_64/pyproject.toml` — pytest configuration

---

## Project Constraints (from CLAUDE.md)

No `CLAUDE.md` found in the repository root. No project-level coding constraints to enforce beyond those stated in the CONTEXT.md locked decisions and the STATE.md project decisions.

---

## Sources

### Primary (HIGH confidence)

- [Textual Documentation — Screens](https://textual.textualize.io/guide/screens/) — push_screen, pop_screen, Screen subclassing
- [Textual Documentation — Input Widget](https://textual.textualize.io/widgets/input/) — password=True, placeholder, validators, value property
- [Textual Documentation — RichLog Widget](https://textual.textualize.io/widgets/rich_log/) — .write() method, ANSI handling
- [Textual — Thread Workers discussion](https://github.com/Textualize/textual/discussions/3788) — @work decorator with subprocess.Popen pattern
- [Docker Compose Variable Interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/) — `${VAR}` syntax, `.env` quoting
- [Docker Compose Startup Ordering](https://docs.docker.com/compose/how-tos/startup-order/) — `depends_on` with `condition: service_healthy`
- [Install Docker Engine on Debian](https://docs.docker.com/engine/install/debian/) — apt commands, arm64 support
- [Install Docker Engine on Ubuntu](https://docs.docker.com/engine/install/ubuntu/) — apt GPG key setup, repository setup
- [Install Docker Engine on Fedora](https://docs.docker.com/engine/install/fedora/) — dnf config-manager commands
- [Docker Engine Linux post-install](https://docs.docker.com/engine/install/linux-postinstall/) — docker group usermod, newgrp behaviour
- [Python secrets module](https://docs.python.org/3/library/secrets.html) — token_hex, cryptographic security rationale
- [Python pathlib documentation](https://docs.python.org/3/library/pathlib.html) — expanduser, resolve, mkdir
- [Python subprocess documentation](https://docs.python.org/3/library/subprocess.html) — Popen, run, check=True
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/) — ~/.config convention
- [BitNest appsettings.json](../../../BitNest/appsettings.json) — Confirmed env var names: ConnectionStrings__DefaultConnection, Auth__SigningKey, UploadsPath
- [BitNest .github/workflows/docker-image.yml](../../../.github/workflows/docker-image.yml) — Confirmed multi-arch linux/amd64,linux/arm64 publish; image tag pattern
- [BitNest compose.yaml](../../../compose.yaml) — Confirmed service names, internal ports, volume mount paths

### Secondary (MEDIUM confidence)

- [PyPI textual](https://pypi.org/project/textual/) — Confirmed 8.1.1 as current latest version (2026-03-26)
- [ArchWiki Docker](https://wiki.archlinux.org/title/Docker) — pacman package name, systemctl required on Arch
- [Compose .env special characters issue](https://github.com/docker/compose/issues/5980) — `$` interpolation in password values confirmed
- [Compose tilde path issue](https://github.com/docker/compose/issues/6506) — `~` not expanded by Compose confirmed
- [readline ANSI prompt bug — CPython issue 17337](https://bugs.python.org/issue17337) — ANSI codes in input() prompt break readline cursor position

### Tertiary (LOW confidence)

- None — all findings have at least MEDIUM confidence backing.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Textual version confirmed via pip index; all stdlib modules verified against docs.python.org; Docker install commands from official Docker docs
- Architecture: HIGH — Textual screen navigation pattern from official docs; compose template pattern from official Docker Compose interpolation docs; subprocess/Popen + @work from Textual GitHub discussion
- Pitfalls: HIGH — 8 of 9 pitfalls verified against official Docker docs or CPython issue tracker; Textual pip dependency pitfall is new (arises from CONTEXT.md Textual decision)
- Integration points: HIGH — env var names confirmed from actual appsettings.json; image names confirmed from actual GitHub Actions workflow

**Research date:** 2026-03-26
**Valid until:** 2026-04-26 (stable domain; Textual releases frequently but 8.x API is stable)

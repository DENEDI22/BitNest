# Architecture Research

**Domain:** Standalone Python installer scripts for self-hosted Docker Compose app
**Researched:** 2026-03-26
**Confidence:** HIGH

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      User invokes installer                      │
│      python install_linux_x86.py install|update|uninstall       │
└──────────────────────────────┬──────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                      Single Python File                          │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │  CLI Layer   │  │  Wizard Layer│  │  State Layer         │   │
│  │  (argparse)  │  │  (prompts)   │  │  (~/.config/bitnest/ │   │
│  │  subcommands │  │  validation  │  │   install.json)      │   │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────┘   │
│         └─────────────────▼──────────────────────┘              │
│                    ┌──────────────┐                              │
│                    │  Core Logic  │                              │
│                    │  install()   │                              │
│                    │  update()    │                              │
│                    │  uninstall() │                              │
│                    └──────┬───────┘                              │
│                           │                                      │
│  ┌────────────────────────▼─────────────────────────────────┐   │
│  │              File Writers                                  │   │
│  │  write_compose_yaml()  write_env_file()                   │   │
│  └────────────────────────┬─────────────────────────────────┘   │
└───────────────────────────┼─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│                  Host Filesystem                                  │
│  ~/bitnest/compose.yaml                                          │
│  ~/bitnest/.env                                                  │
│  ~/.config/bitnest/install.json                                  │
│  <DATA_DIR>/  (bind mount root, user-chosen)                     │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│                 subprocess → docker compose                       │
│  docker compose -f ~/bitnest/compose.yaml up -d                  │
│  docker compose -f ~/bitnest/compose.yaml pull                   │
│  docker compose -f ~/bitnest/compose.yaml down                   │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Implementation |
|-----------|----------------|----------------|
| CLI Layer | Parse subcommands and flags via argparse | `build_parser()` returns `ArgumentParser` with `install`, `update`, `uninstall` subparsers |
| Wizard Layer | Interactive prompts for config, validation | Plain `input()` + `getpass.getpass()`, no external deps |
| State Layer | Read/write install.json at `~/.config/bitnest/` | `json` stdlib, `pathlib.Path` |
| File Writers | Emit compose.yaml and .env to install dir | Python triple-quoted string constants, `str.format()` substitution |
| Core Logic | Orchestrate steps for each subcommand | Functions `do_install()`, `do_update()`, `do_uninstall()` |
| Subprocess | Call `docker compose` commands on host | `subprocess.run()` with `-f` flag pointing at install dir |

---

## Recommended Project Structure

```
installers/
├── install_linux_x86.py      # Linux x86_64 installer (phase 10)
├── install_linux_arm64.py    # Linux ARM64 / Raspberry Pi (phase 11)
└── install_windows_wsl.py    # Windows WSL2 (phase 12)
```

Each file is completely self-contained. No shared module, no `__init__.py`, no imports beyond stdlib. This is the correct trade-off: small duplication in exchange for a single-file drop that works with `python3 installer.py install` and nothing else.

### Structure Rationale

- **Single file per platform:** User downloads one file, runs it. No directory tree to clone. Matches how tools like `get-pip.py` and `rustup-init` work in practice.
- **No shared module:** The installer is distributed as a script, not a package. A shared module would require the user to download multiple files and maintain relative imports — complexity that defeats the purpose.
- **Naming encodes platform:** `install_linux_x86.py` is unambiguous at a glance; a runtime `platform.machine()` check inside one file would silently do the wrong thing if someone runs the wrong copy.

---

## Architectural Patterns

### Pattern 1: Embed compose.yaml as a Python String Constant

**What:** The installer writes compose.yaml by holding it as a triple-quoted string constant and calling `str.format()` to inject values collected from the wizard.

**When to use:** Always, for stdlib-only single-file installers. Do not read compose.yaml from disk; the installer must be self-contained.

**Trade-offs:**
- Pro: Zero file dependencies — one script = complete installer.
- Pro: `str.format()` is cleaner than `string.Template` because compose.yaml itself uses `${VAR}` syntax. `string.Template` treats `$` as its own placeholder delimiter and would require escaping every `${...}` in the template as `$${...}`. `str.format()` uses `{name}` delimiters, so `${VAR}` in the compose text passes through untouched.
- Con: The compose template is frozen at script release time; updating the template requires releasing a new installer version (acceptable — that is the intended workflow).

**Example:**

```python
# compose.yaml content uses Docker's ${VAR} syntax freely.
# Python's str.format() only substitutes {PYTHON_VAR} placeholders.
# Double-brace {{DATA_DIR}} in Python source becomes ${DATA_DIR} after format().
COMPOSE_TEMPLATE = """\
services:
  api:
    image: {docker_hub_user}/bitnest_api:latest
    container_name: bitnest_api
    depends_on:
      - db
    env_file:
      - .env
    ports:
      - "{api_port}:8080"
    volumes:
      - ${{DATA_DIR}}/storage:/app/data/storage

  db:
    image: postgres:16
    container_name: bitnest_db
    env_file:
      - .env
    volumes:
      - ${{DATA_DIR}}/pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  frontend:
    image: {docker_hub_user}/bitnest_frontend:latest
    container_name: bitnest_frontend
    ports:
      - "{frontend_port}:80"
"""
# Calling COMPOSE_TEMPLATE.format(docker_hub_user="johndoe", api_port="5000",
# frontend_port="3000") yields valid compose.yaml with ${DATA_DIR} intact
# for Docker Compose to resolve from .env at runtime.
```

---

### Pattern 2: .env File Format and Bind Mount Integration

**What:** The installer writes a `.env` file alongside `compose.yaml`. Docker Compose automatically loads it when `compose.yaml` is in the same directory and `-f` points to that file.

**When to use:** Use `.env` for all runtime configuration that must survive container restarts and be editable by the user post-install. This includes the bind mount root path, DB credentials, and the application secret key.

**Bind mount path approach: use `.env` + `${VAR}` in compose.yaml, not a hardcoded absolute path.** This is the correct choice because:
1. The user chooses the data directory during the wizard; it cannot be known at script-write time.
2. A path in `.env` is visible and editable without touching compose.yaml.
3. Docker Compose expands `${DATA_DIR}` from `.env` before resolving the mount — documented, production-safe behaviour.

**Key rule:** The installer must resolve `~` to an absolute path using `Path.home()` before writing to `.env`. Docker Compose does not expand `~` in variable values — only absolute paths work in volume definitions.

**Key rule:** Write values unquoted unless they contain spaces. Unquoted and double-quoted values undergo Docker Compose interpolation; single-quoted values are treated literally (no `${VAR}` expansion).

**Example .env file written by installer:**

```
# BitNest configuration - generated by installer
POSTGRES_USER=bitnest
POSTGRES_PASSWORD=s3cur3pass
POSTGRES_DB=bitnest
DATA_DIR=/home/alice/bitnest-data
BITNEST_SECRET_KEY=a7f3...64chars
```

**Example writer function:**

```python
def write_env_file(path: Path, cfg: dict) -> None:
    lines = [
        "# BitNest configuration - generated by installer",
        f"POSTGRES_USER={cfg['db_user']}",
        f"POSTGRES_PASSWORD={cfg['db_password']}",
        f"POSTGRES_DB={cfg['db_name']}",
        f"DATA_DIR={cfg['data_dir']}",   # resolved absolute path, no ~
        f"BITNEST_SECRET_KEY={cfg['secret_key']}",
    ]
    path.write_text("\n".join(lines) + "\n")
```

---

### Pattern 3: State File at `~/.config/bitnest/install.json`

**What:** After a successful install, write a small JSON state file so `update` and `uninstall` subcommands know where Docker Compose files are and what install dir to operate on.

**When to use:** Always. Without state, `update` must re-prompt for configuration that was already collected during install, which is hostile UX.

**Location:** `~/.config/bitnest/install.json`

Follow XDG Base Directory Specification: check `os.environ.get("XDG_CONFIG_HOME")` first, fall back to `~/.config`. Do not store state inside the install directory — `uninstall` deletes the install directory, and if state is inside it the last step of cleanup cannot run.

**Schema:**

```json
{
  "version": "v0.1.0",
  "install_dir": "/home/alice/bitnest",
  "data_dir": "/home/alice/bitnest-data",
  "installed_at": "2026-03-26T14:00:00Z",
  "platform": "linux_x86_64"
}
```

`install_dir` points to the directory containing `compose.yaml` and `.env`. This is the only value needed to reconstruct the `-f` path for all subsequent `docker compose` calls.

**Example reader/writer:**

```python
import json, os
from pathlib import Path

def state_path() -> Path:
    base = Path(os.environ.get("XDG_CONFIG_HOME") or Path.home() / ".config")
    return base / "bitnest" / "install.json"

def write_state(data: dict) -> None:
    p = state_path()
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(data, indent=2))

def read_state() -> dict | None:
    p = state_path()
    if not p.exists():
        return None
    return json.loads(p.read_text())
```

---

### Pattern 4: subprocess — Use `-f` Flag, Not `cwd`

**What:** Run all `docker compose` commands by passing `-f <abs_path_to_compose.yaml>` rather than changing working directory with `cwd=`.

**When to use:** Always, for installer scripts. The installer may be run from any directory; relying on `cwd` being the install directory is a silent failure path.

**Why `-f` beats `cwd`:**
- Docker Compose resolves `.env` relative to the directory containing the `-f` file, not the process `cwd`. So `-f ~/bitnest/compose.yaml` automatically loads `~/bitnest/.env`. This is the documented behaviour.
- `-f` with an absolute path is explicit and reproducible regardless of where the installer is invoked from.

**Compose binary detection:** `docker compose` (plugin, V2) is the current standard. `docker-compose` (standalone V1) is deprecated. Detect in order: try `docker compose version` first; if it fails, try `docker-compose --version`; if both fail, abort with install instructions.

**Example subprocess wrapper:**

```python
import subprocess
from pathlib import Path

def compose_run(install_dir: Path, *args: str) -> None:
    """Run a docker compose command against the install directory."""
    compose_file = install_dir / "compose.yaml"
    cmd = _compose_binary() + ["-f", str(compose_file)] + list(args)
    subprocess.run(cmd, check=True)

def _compose_binary() -> list[str]:
    """Return ['docker', 'compose'] or ['docker-compose'] depending on what is installed."""
    try:
        subprocess.run(
            ["docker", "compose", "version"],
            check=True, capture_output=True
        )
        return ["docker", "compose"]
    except (subprocess.CalledProcessError, FileNotFoundError):
        pass
    try:
        subprocess.run(
            ["docker-compose", "--version"],
            check=True, capture_output=True
        )
        return ["docker-compose"]
    except (subprocess.CalledProcessError, FileNotFoundError):
        raise RuntimeError(
            "Docker Compose not found. Install Docker Desktop or the Compose plugin."
        )
```

**For interactive output (up, pull):** Do not use `capture_output=True` — let stdout/stderr stream directly to the terminal so the user sees pull progress. Use `capture_output=True` only for version checks and silent queries.

---

### Pattern 5: argparse Subcommand Structure

**What:** Use `add_subparsers()` with `set_defaults(func=...)` so each subcommand dispatches to its own handler function without a chain of `if/elif` blocks.

**When to use:** Always for three or more subcommands. The `set_defaults` dispatch pattern is the idiomatic argparse approach and avoids manual `args.subcommand == "install"` comparisons.

**Example:**

```python
import argparse

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="bitnest-installer",
        description="Install, update, or remove BitNest"
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_install = sub.add_parser("install", help="Install BitNest")
    p_install.add_argument("--dir", default="~/bitnest",
                           help="Directory to write compose files")
    p_install.add_argument("--data-dir", default="~/bitnest-data",
                           help="Directory for persistent data")
    p_install.set_defaults(func=do_install)

    p_update = sub.add_parser("update", help="Pull latest images and restart")
    p_update.set_defaults(func=do_update)

    p_uninstall = sub.add_parser("uninstall", help="Stop and remove BitNest")
    p_uninstall.add_argument("--purge-data", action="store_true",
                             help="Also delete the data directory")
    p_uninstall.set_defaults(func=do_uninstall)

    return parser

def main() -> None:
    parser = build_parser()
    args = parser.parse_args()
    args.func(args)   # dispatches to do_install / do_update / do_uninstall
```

---

## Data Flow

### Install Flow

```
User: python install_linux_x86.py install [--dir] [--data-dir]
    |
    v
argparse -> do_install(args)
    |
    +-- check_docker()              abort if Docker not present
    +-- run_wizard(args)            collect DB password, secret key, ports
    |    returns cfg: dict
    +-- resolve paths               Path(args.dir).expanduser().resolve()
    +-- install_dir.mkdir(parents=True, exist_ok=True)
    +-- data_dir.mkdir(parents=True, exist_ok=True)
    +-- write_compose_yaml(install_dir / "compose.yaml", cfg)
    +-- write_env_file(install_dir / ".env", cfg)
    +-- compose_run(install_dir, "pull")
    +-- compose_run(install_dir, "up", "-d")
    +-- write_state({version, install_dir, data_dir, platform, installed_at})
        print success message + access URL
```

### Update Flow

```
User: python install_linux_x86.py update
    |
    v
do_update(args)
    +-- read_state()     abort with message if not installed
    +-- compose_run(install_dir, "pull")
    +-- compose_run(install_dir, "up", "-d", "--remove-orphans")
    +-- write_state({...state with updated version...})
```

### Uninstall Flow

```
User: python install_linux_x86.py uninstall [--purge-data]
    |
    v
do_uninstall(args)
    +-- read_state()     abort with message if not installed
    +-- compose_run(install_dir, "down", "--remove-orphans")
    +-- if args.purge_data: shutil.rmtree(data_dir)
    +-- shutil.rmtree(install_dir)
    +-- state_path().unlink()     state deleted last
```

---

## Integration Points

### Existing compose.yaml (TEMPLATE DIVERGES — repo file unchanged)

The current `compose.yaml` at repo root uses:
- `build: ./BitNest` and `build: ./FrontEnd` — local builds for developers.
- Named volumes `bit_storage` and `pgdata` — Docker-managed, opaque location.

The installer template diverges from the repo `compose.yaml` in two explicit ways:

| Concern | Repo compose.yaml (developer) | Installer compose.yaml (end-user) |
|---------|-------------------------------|-----------------------------------|
| Images | `build: ./BitNest` (local build) | `image: {user}/bitnest_api:latest` (Docker Hub pull) |
| Storage | Named volumes (`bit_storage`, `pgdata`) | Bind mounts to `${DATA_DIR}/storage` and `${DATA_DIR}/pgdata` |

The repo `compose.yaml` is not modified. The installer embeds its own template as a string constant in the Python file. These are two separate files serving different audiences.

### CI/CD (no changes required)

The existing GitHub Actions pipeline pushes `{user}/bitnest_api:latest` and `{user}/bitnest_frontend:latest` to Docker Hub. The installer `image:` references consume exactly these tags. No CI changes are needed for phases 10-12.

### .env Auto-Loading by Docker Compose

Docker Compose V2 automatically loads `.env` from the same directory as the compose file when invoked with `-f <path>`. The installer places both files in `install_dir`. No `--env-file` flag is required.

---

## Named Volume vs Bind Mount Decision

| Criterion | Named Volume | Bind Mount |
|-----------|-------------|------------|
| Backups | Hard (data inside Docker-managed dir) | Easy (user knows exact path) |
| Raspberry Pi (SD card / external disk) | Hard | Easy |
| Docker auto-creates dir | Yes | No (installer must mkdir) |
| Compose portability | High (no absolute paths in compose.yaml) | Medium (path in .env) |
| Uninstall data removal | Requires `docker volume rm` | `shutil.rmtree(data_dir)` |

**Decision: bind mounts for the installer.** Self-hosted users on home servers and Raspberry Pis need transparent, accessible paths for backups and migrations. The installer creates the directory; the uninstall command can remove it. Named volumes remain in the developer `compose.yaml` at repo root — they are appropriate there because developers do not need to inspect volume internals.

---

## Build Order for Phases 10-12

### Phase 10: Linux x86_64 Installer (`install_linux_x86.py`)

Build this first. It establishes every shared pattern:
- Full argparse structure with install/update/uninstall
- compose.yaml string template with `str.format()` substitution
- .env writer
- State file read/write at `~/.config/bitnest/`
- `_compose_binary()` detection (V2 plugin vs V1 standalone)
- Docker auto-install via apt (Debian/Ubuntu) or Docker convenience script

All platform-specific code is Linux x86_64. Tests can run in a standard Ubuntu VM or container.

### Phase 11: Linux ARM64 Installer (`install_linux_arm64.py`)

Start from a copy of the x86_64 installer. The differences are:
- Docker install path differs (ARM64 apt repo, or Docker convenience script — same URL, architecture-detected automatically by the script).
- Image architecture is handled transparently by Docker Hub multi-arch manifests for `postgres:16`. BitNest images need ARM64 manifests published by CI (a separate concern, but the compose template is identical).
- Test on Raspberry Pi OS or a QEMU ARM64 environment.

Phase 11 depends on Phase 10 being stable so the shared patterns are frozen before copying.

### Phase 12: Windows WSL2 Installer (`install_windows_wsl.py`)

Most divergent platform. Key differences:
- Docker Desktop on Windows cannot be auto-installed from a script (requires a GUI installer or winget). The wizard detects and provides instructions rather than auto-installs.
- Path handling: WSL2 Python sees Linux paths (`/home/user/`). The installer should accept and write any valid WSL2 absolute path. Windows drive paths (`/mnt/c/bitnest-data`) work as bind mounts.
- The `docker compose` call is identical to Linux — Docker Desktop's WSL integration puts `docker` on the WSL2 PATH.
- State file location is `~/.config/bitnest/install.json` within the WSL2 environment (same XDG pattern).

Phase 12 can be developed in parallel with Phase 11 if two engineers are available; there is no technical dependency between them once Phase 10 patterns are established.

---

## Anti-Patterns

### Anti-Pattern 1: Using `string.Template` for compose.yaml Embedding

**What people do:** `from string import Template; Template(compose_text).substitute(cfg)`

**Why it is wrong:** Docker Compose uses `${VAR}` and `$VAR` syntax in compose files. `string.Template` interprets `$` as its own placeholder delimiter. Every `${POSTGRES_PASSWORD}` in the compose template becomes a Template placeholder and raises `KeyError` unless escaped as `$${POSTGRES_PASSWORD}`. The escaping burden on every Docker-style variable is high and error-prone.

**Do this instead:** Use `str.format()` with `{python_var}` for installer-time substitution, and double-brace `{{DATA_DIR}}` in the Python source to produce literal `${DATA_DIR}` after `.format()` runs — leaving Docker Compose variables intact.

---

### Anti-Pattern 2: Writing Absolute Paths Directly into compose.yaml

**What people do:** Inline the user's data directory as a literal path:
```yaml
volumes:
  - /home/alice/bitnest-data/storage:/app/data/storage
```

**Why it is wrong:** If the user moves the data directory, they must edit compose.yaml and understand YAML syntax. The path is duplicated between compose.yaml and any state record. Running `update` would need to regenerate compose.yaml or the path goes stale.

**Do this instead:** Write `${DATA_DIR}/storage:/app/data/storage` in compose.yaml and `DATA_DIR=/home/alice/bitnest-data` in `.env`. One edit location, clear semantics, compose.yaml is path-agnostic.

---

### Anti-Pattern 3: Using `cwd=install_dir` Instead of `-f`

**What people do:** `subprocess.run(["docker", "compose", "up", "-d"], cwd=install_dir)`

**Why it is wrong:** Works until the user or a script changes the working directory, then breaks silently. Docker Compose discovers `.env` relative to the compose file location when `-f` is used — relying on `cwd` does not guarantee `.env` is found.

**Do this instead:** `subprocess.run(["docker", "compose", "-f", str(compose_file), "up", "-d"], check=True)`.

---

### Anti-Pattern 4: Storing State in the Install Directory

**What people do:** Write `install.json` next to `compose.yaml` in `~/bitnest/`.

**Why it is wrong:** `uninstall` deletes the install directory. If state is inside it, state is gone before the final cleanup step runs (data directory removal, etc.), creating a partial-uninstall failure mode.

**Do this instead:** State always lives at `~/.config/bitnest/install.json`, separate from the install directory. `uninstall` deletes the state file as its last action, after all other cleanup has succeeded.

---

### Anti-Pattern 5: Modifying the Repo compose.yaml for Installer Needs

**What people do:** Edit the existing `compose.yaml` to replace `build:` with `image:` references and named volumes with bind mounts so both developers and the installer use the same file.

**Why it is wrong:** Breaks the local developer workflow — `build:` directives are gone, named volumes are replaced. Couples developer and end-user deployment concerns into one file.

**Do this instead:** The installer embeds its own compose template as a string constant. The repo `compose.yaml` remains the developer file, untouched by this milestone.

---

## Scaling Considerations

This is a single-user self-hosted deployment. "Scaling" means surviving real-world failure modes, not traffic load.

| Concern | Mitigation |
|---------|------------|
| Installer re-run on existing install | `do_install` checks for existing state file; prompts to confirm overwrite; backs up existing .env |
| Docker not installed at install time | `check_docker()` detects and runs platform-specific install; aborts with clear instructions on failure |
| User changes data dir post-install | Not supported in v1 — document that data migration requires manual steps; update subcommand does not re-prompt for paths |
| compose.yaml template drift | Template is frozen in installer; new installer version ships new template; users re-run `install` to migrate |
| Multiple BitNest installs on same host | Not supported in v1 — single state file, single install dir; document as known limitation |

---

## Sources

- [Docker Compose Variable Interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/) — HIGH confidence
- [Docker Compose Environment Variables](https://docs.docker.com/compose/how-tos/environment-variables/set-environment-variables/) — HIGH confidence
- [Python argparse documentation](https://docs.python.org/3/library/argparse.html) — HIGH confidence
- [Python string.Template](https://docs.python.org/3/library/string.html#template-strings) — HIGH confidence
- [Python subprocess documentation](https://docs.python.org/3/library/subprocess.html) — HIGH confidence
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/) — HIGH confidence
- [Docker Volumes documentation](https://docs.docker.com/engine/storage/volumes/) — HIGH confidence
- [Docker Compose install overview](https://docs.docker.com/compose/install/) — HIGH confidence

---
*Architecture research for: BitNest Python installer scripts*
*Researched: 2026-03-26*

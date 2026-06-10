# Pitfalls Research

**Domain:** Python installer scripts for self-hosted Docker Compose apps (Linux x86_64, ARM64, Windows WSL2)
**Researched:** 2026-03-26
**Confidence:** HIGH

---

## Critical Pitfalls

### Pitfall 1: Docker Group Membership Not Active in the Same Script Session

**What goes wrong:**
The installer calls `usermod -aG docker $USER` to add the current user to the docker group, then
immediately tries to run `docker compose up`. The call fails with
`permission denied while trying to connect to the Docker daemon socket`. The group addition succeeded,
but the kernel security token for the running process still reflects the old membership snapshot taken
at login time.

**Why it happens:**
UNIX group memberships are read at login and embedded in the process credential set. `usermod` updates
`/etc/group`, but the already-running Python process (and any subprocesses it spawns) does not re-read
that file. `newgrp docker` opens a subshell with the new GID active, but that subshell is a child of
the installer and cannot propagate back to the parent. The installer's `subprocess.run(['docker', ...])`
therefore inherits the original (docker-less) credentials.

**How to avoid:**
Two safe patterns:

1. **Invoke docker via sudo for the remainder of the install session.** After `usermod`, call all
   `docker`/`docker compose` commands in that session with `sudo -n docker ...` or
   `sudo -n docker compose ...`. Print a clear message at the end telling the user to log out and back
   in so future manual use of docker works without sudo.

2. **Re-exec the installer under `sg docker`** if the group is newly added:
   ```python
   import os, sys
   os.execvp("sg", ["sg", "docker", "-c", " ".join([sys.executable] + sys.argv)])
   ```
   `os.execvp` replaces the current process image, so the new process starts with the docker GID
   active. Guard this with a flag (`--docker-group-active`) so the re-exec does not loop.

Do NOT use `subprocess.run(['newgrp', 'docker'])` — it opens an interactive subshell that blocks the
installer.

**Warning signs:**
`permission denied` on the very first `docker compose` call immediately after the Docker install step
inside the same script run.

**Phase to address:**
Linux x86_64 installer core phase; also applies to ARM64 phase.

---

### Pitfall 2: Docker Compose V1 Binary Absence Breaking the Installer

**What goes wrong:**
The installer checks for Docker Compose by running `docker-compose --version` (hyphen form). On any
system where only the Docker Compose V2 plugin is installed (the default since Docker Engine 23.0,
June 2023), this returns command-not-found and the installer aborts or falls back incorrectly.

**Why it happens:**
Docker Compose V1 (`docker-compose` standalone binary, Python-based) reached end-of-life in June 2023
and is no longer shipped by Docker. Docker Compose V2 ships as a CLI plugin and is invoked as
`docker compose` (space, not hyphen). Package managers and Docker's own install scripts install only
V2 now. The `version:` top-level key in `compose.yaml` is also silently ignored in V2 but may cause
warnings that confuse users.

**How to avoid:**
- Detect compose by running `docker compose version` (space form) and parsing the output.
- Never invoke the hyphen form in the installer. If you want to remain tolerant, check both in order:
  first `docker compose version`, then `docker-compose --version` as a fallback, and emit a warning
  if only the fallback is found.
- Do not include a top-level `version:` key in the generated `compose.yaml`; it is obsolete in V2 and
  Docker now issues deprecation warnings for it.

**Warning signs:**
`command not found: docker-compose`; `compose.yaml` file contains `version: "3.x"` at the top level.

**Phase to address:**
Docker detection/install step across all three platform installers.

---

### Pitfall 3: Special Characters in Generated Passwords Breaking .env Variable Interpolation

**What goes wrong:**
The installer generates a random database password such as `P@$$w0rd#42!` and writes it to `.env`.
Docker Compose reads `.env` and performs shell-style variable interpolation: `$` is treated as the
start of a variable reference, `$$` becomes a literal `$`. The password is injected into the
PostgreSQL connection string inside `compose.yaml` with `${DB_PASSWORD}`, and the resulting expanded
value has mangled characters. The database starts with the wrong password while the API uses the
original one.

**Why it happens:**
Docker Compose V2 always performs variable interpolation on `.env` values when they are referenced in
`compose.yaml` via `${VAR}` syntax. The interpolation engine treats bare `$` in values as variable
references unless the `.env` parser itself treats the value as literally quoted. The V2 `.env` parser
respects single quotes to suppress interpolation, but this behaviour changed between minor Compose
versions and is not universally relied upon.

**How to avoid:**
- **Avoid special characters entirely in generated secrets.** Use `secrets.token_hex(32)` which
  produces only `[0-9a-f]` characters — no interpolation-hazardous chars at all. This is the cleanest
  solution.
- If alphanumeric passwords are required (e.g., for aesthetics), use `secrets.token_urlsafe(32)` which
  produces `[A-Za-z0-9_-]` — still safe.
- Do NOT generate passwords with `random.random()` or `random.randint()` — the `random` module uses
  Mersenne Twister which is not cryptographically secure and is unsuitable for secrets.
- Do NOT use `os.urandom(n)` directly; `secrets.token_hex` is `os.urandom` wrapped in a
  hex-encoding convenience function and is the correct stdlib API.
- If you must write a password with `$` to `.env`, write it wrapped in single quotes in the `.env`
  file: `DB_PASSWORD='P@$$w0rd'`. Verify with `docker compose config` before starting containers.

**Warning signs:**
Database container starts; API container fails to connect with "authentication failed for user" or
"password authentication failed"; `docker compose config` shows a truncated or mangled password value.

**Phase to address:**
Config wizard / .env generation step in all platform installers.

---

### Pitfall 4: Tilde and Relative Paths in compose.yaml Volume Mounts Not Expanding

**What goes wrong:**
The installer writes a volume mount like `~/bitnest/data:/app/data` or `./data:/app/data` into
`compose.yaml`. Docker Compose V2 does NOT expand `~` (tilde) — it is passed literally to the
container runtime which rejects it or creates a directory literally named `~`. Relative paths (`./`)
are resolved relative to the compose file location in V2, which works, but only when the user runs
`docker compose` from the correct directory.

**Why it happens:**
Docker Compose delegates volume path resolution to the container runtime (containerd/runc). The shell
never processes the path. Tilde expansion is a shell feature, not a filesystem feature. Docker Compose
V2 does expand `${COMPOSE_PROJECT_DIR}` and environment variable references inside paths, but not
the `~` shorthand.

**How to avoid:**
- In the installer's Python code, resolve all user-provided paths to absolute paths before writing
  them to `compose.yaml`:
  ```python
  import os
  abs_path = os.path.abspath(os.path.expanduser(user_input))
  ```
- Use `os.path.expanduser` explicitly; do not rely on the shell or Compose to expand `~`.
- Never write tilde or relative paths into generated `compose.yaml` files.
- For volume paths derived from the compose file location, `${COMPOSE_PROJECT_DIR}` is safe to use.

**Warning signs:**
`Error response from daemon: invalid mount config for type "bind": invalid mount path: '~' must be
absolute`; a literal directory named `~` appears in the filesystem.

**Phase to address:**
Config wizard / compose.yaml generation in all platform installers.

---

### Pitfall 5: WSL2 — Docker Desktop Not Running When Installer Executes

**What goes wrong:**
On Windows WSL2, Docker is provided by Docker Desktop running on the Windows host. The installer
checks `docker info` or `docker version` to verify Docker is available, gets `Cannot connect to the
Docker daemon`, and incorrectly concludes Docker is not installed. It then tries to install Docker
Engine inside WSL2 via `apt`, which conflicts with Docker Desktop's WSL integration.

**Why it happens:**
Docker Desktop for Windows exposes the Docker socket into WSL2 distros via a named pipe bridge only
while Desktop is running. If Docker Desktop is closed or has not yet started, the socket
`/var/run/docker.sock` is absent or unresponsive even though Docker is "installed". The installer
cannot distinguish between "not installed" and "installed but not running".

**How to avoid:**
- On the WSL2 installer: detect WSL2 first (`/proc/version` contains `microsoft` or `WSL`).
- Distinguish the check: attempt `docker info` and inspect the error. If the error is a socket
  connection error (not a "not found" error), tell the user Docker Desktop is not running rather than
  attempting to install Docker.
- Guide the user to start Docker Desktop on the Windows side, then press Enter to retry — do not
  auto-install Docker Engine into WSL2 unless the user explicitly chooses that path.
- Print the Docker Desktop download URL if Docker is genuinely absent:
  `https://docs.docker.com/desktop/install/windows-install/`

**Warning signs:**
`Cannot connect to Docker daemon at unix:///var/run/docker.sock` on a machine where `which docker`
returns a valid path; `/proc/version` contains `microsoft`.

**Phase to address:**
WSL2 installer platform detection step.

---

### Pitfall 6: WSL2 — systemd Not Available, Service Commands Fail

**What goes wrong:**
The installer runs `sudo systemctl start docker` or `sudo service docker start` after installing
Docker Engine inside WSL2. On WSL2 without systemd enabled (the default on Windows 10 and older
WSL2 setups), `systemctl` is either absent or non-functional, causing the Docker daemon to never start.

**Why it happens:**
systemd requires WSL2 with `systemd=true` in `/etc/wsl.conf`, and this feature requires Windows 11
22H2 or later. Windows 10 WSL2 instances never have systemd. Even on Windows 11 the feature must be
explicitly enabled. Scripts that assume `systemctl` works will fail silently or produce confusing
errors.

**How to avoid:**
- Before invoking `systemctl`, check for systemd: `os.path.exists('/run/systemd/private')` or
  `subprocess.run(['systemctl', '--version'], capture_output=True).returncode == 0`.
- If systemd is absent, start the Docker daemon directly:
  `sudo dockerd &` or use the `service` sysvinit wrapper if present.
- In the WSL2 installer, favor the Docker Desktop integration path (no daemon management needed) over
  installing Docker Engine. This avoids the systemd problem entirely.

**Warning signs:**
`System has not been booted with systemd as init system (PID 1). Can't operate.`; `dockerd` is not
running after install.

**Phase to address:**
WSL2 installer Docker daemon start/verify step.

---

### Pitfall 7: WSL2 — Volume Paths on /mnt/c/ Causing Severe Performance and Permission Issues

**What goes wrong:**
The installer places the BitNest data directory at a Windows-accessible path such as
`/mnt/c/Users/name/bitnest`. Docker bind mounts under `/mnt/c/` go through the 9P filesystem
translation layer between WSL2 and Windows NTFS. File I/O is 10-100x slower, and POSIX permission
bits are approximated. The PostgreSQL container may fail to start because it cannot set the required
permissions on its data directory under this path.

**Why it happens:**
WSL2 mounts the Windows filesystem under `/mnt/c/` (and other drive letters). Docker Desktop routes
bind mounts through this bridge. PostgreSQL's startup requires `chmod 700` on its data directory and
rejects directories it does not own. NTFS does not support these semantics natively, so the operation
fails or is silently ignored.

**How to avoid:**
- Default all data directories to paths inside the WSL2 Linux filesystem: `~/bitnest/` resolves to
  `/home/<user>/bitnest/` (true ext4), not `/mnt/c/...`.
- In the WSL2 installer, warn the user if they choose a path under `/mnt/` and recommend against it.
- Expand and validate the chosen path's filesystem with a quick `stat --file-system --format=%T`
  check and warn if it is `9p`.

**Warning signs:**
`initdb: error: could not change permissions of directory ... Permission denied` in the PostgreSQL
container log; extremely slow file operations.

**Phase to address:**
WSL2 installer config wizard / path selection step.

---

### Pitfall 8: ARM64 — Using Debian/Ubuntu x86_64 APT Repository for Docker Instead of ARM-Compatible Repo

**What goes wrong:**
The installer adds Docker's APT repository using a hardcoded `amd64` architecture line:
`deb [arch=amd64] https://download.docker.com/linux/debian ...`. On an ARM64 host (Raspberry Pi),
`apt update` fetches package metadata but `apt install docker-ce` fails because no `amd64` packages
match the `arm64` system.

**Why it happens:**
Many Docker install tutorials copy-paste the x86_64 command. The ARM64 installer must use
`arch=arm64` in the repository line and must fetch the correct GPG key path.

**How to avoid:**
- Detect architecture at runtime: `platform.machine()` returns `aarch64` on ARM64.
- Dynamically set the `arch` value in the APT source line:
  - `amd64` for `x86_64`/`amd64`
  - `arm64` for `aarch64`
- For Raspberry Pi OS, follow the Docker docs for Debian (not the Raspberry Pi OS 32-bit page) when
  running 64-bit OS. Raspberry Pi OS 64-bit (bookworm) is Debian bookworm under the hood; the Debian
  Docker repo applies.
- Pin to `bookworm` or `bullseye` by reading `/etc/os-release` (`VERSION_CODENAME`) rather than
  hardcoding the release name.

**Warning signs:**
`E: Unable to locate package docker-ce`; `N: Skipping acquire of configured file ... as repository
doesn't support architecture arm64`.

**Phase to address:**
ARM64 installer Docker install step.

---

### Pitfall 9: ARM64 — Docker Hub Image Not Published for linux/arm64

**What goes wrong:**
The installer pulls BitNest's images from Docker Hub. If the GitHub Actions build pipeline only
publishes `linux/amd64` manifests, the `docker compose pull` on a Raspberry Pi fails with:
`no matching manifest for linux/arm64/v8 in the manifest list entries`.

**Why it happens:**
`docker buildx build --push` must explicitly list `--platform linux/amd64,linux/arm64` to create a
multi-platform manifest. If this flag is absent or only `linux/amd64` is listed, Docker Hub stores a
single-arch manifest. Docker on ARM64 then rejects the pull.

**How to avoid:**
- The GitHub Actions workflow that publishes BitNest images must use `docker/build-push-action` with
  `platforms: linux/amd64,linux/arm64` and `docker/setup-qemu-action` for cross-compilation.
- The ARM64 installer should verify image availability before starting: run
  `docker manifest inspect <image>` and check that `linux/arm64` appears in the output, giving a
  clear error and link to the project releases page if it does not.

**Warning signs:**
`no matching manifest for linux/arm64/v8`; `docker pull` succeeds on x86_64 but fails on Pi with the
same tag.

**Phase to address:**
ARM64 installer image-pull verification step; also a CI/GitHub Actions concern for the build pipeline.

---

### Pitfall 10: Container Startup Race Condition — API Starts Before PostgreSQL Is Ready

**What goes wrong:**
`docker compose up -d` starts all containers in parallel (or with only dependency ordering based on
`depends_on` with no condition). The API container starts, attempts to open a database connection, and
fails because PostgreSQL is still initializing its data directory. The API crashes and Docker Compose
may or may not restart it. The user sees the web UI but every API call returns 500.

**Why it happens:**
`depends_on` without a `condition` only guarantees that the `db` container has *started*, not that
PostgreSQL is *accepting connections*. PostgreSQL initialization (especially on first run when it
creates the database, runs migrations, etc.) takes several seconds after the container is running.

**How to avoid:**
Add a healthcheck to the `db` service and use `condition: service_healthy` in the `api` depends_on:

```yaml
services:
  db:
    image: postgres:17
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER} -d ${DB_NAME}"]
      interval: 5s
      timeout: 3s
      retries: 10
      start_period: 10s

  api:
    depends_on:
      db:
        condition: service_healthy
```

The installer should generate `compose.yaml` with this healthcheck pattern pre-included rather than
leaving it to the user to add.

**Warning signs:**
API container logs show `connection refused` or `dial: no route to host` pointing at the db hostname
in the first 10-30 seconds after `docker compose up`; API container restarts 1-3 times before
stabilising.

**Phase to address:**
compose.yaml template generation step, all platform installers.

---

### Pitfall 11: Port Conflict — Default Ports Already Bound on the Host

**What goes wrong:**
BitNest binds host ports (e.g., 5432 for PostgreSQL, 5000 for the API, 80/443 for Nginx). If the
host already has a PostgreSQL instance, a local dev server, or another web server on those ports,
`docker compose up` fails with `bind: address already in use`. The error message from Docker is
clear but only appears after the user has gone through the entire config wizard.

**Why it happens:**
Pre-flight checks are skipped. VPS images often ship with PostgreSQL running by default. Developers
commonly have local PostgreSQL, Node, or Python dev servers running. Port 80 is often taken by Apache
or Nginx on fresh server installs.

**How to avoid:**
Run port availability checks early in the installer before the config wizard begins:

```python
import socket

def is_port_free(port: int) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        try:
            s.bind(("0.0.0.0", port))
            return True
        except OSError:
            return False
```

Check all ports the compose stack will bind. If a port is taken, prompt the user for an alternative
port and write it into `.env` / `compose.yaml`. Do not silently skip the check.

**Warning signs:**
`Error response from daemon: Ports are not available: exposing port TCP 0.0.0.0:5432 -> 0.0.0.0:0:
listen tcp 0.0.0.0:5432: bind: address already in use` immediately on `docker compose up`.

**Phase to address:**
Pre-flight checks step, all platform installers (run before config wizard).

---

### Pitfall 12: Installer Running as root — Compose Files and Data Directories Owned by Root

**What goes wrong:**
The user runs the installer with `sudo python3 install.py`. The installer creates the BitNest
directory, writes `compose.yaml`, `.env`, and the data directory as `root:root`. Later the user tries
to edit `.env` or inspect files and finds they need sudo for everything. Worse, if the PostgreSQL
data directory is owned by root, the `postgres` container user (UID 999) cannot write to it and the
database fails to start.

**Why it happens:**
`sudo` changes the effective UID to 0. All files created via `open()`, `os.makedirs()`, etc. in that
context are owned by root unless the installer explicitly calls `os.chown()` to restore ownership.

**How to avoid:**
- Design the installer to run as the target (non-root) user and escalate only the specific operations
  that require root (package installation, `usermod`, Docker daemon start).
- Use targeted `subprocess.run(['sudo', 'apt', 'install', ...])` for system-level steps rather than
  requiring the whole script to run as root.
- If the installer must run as root (e.g., the user explicitly does `sudo ...`), detect this via
  `os.getuid() == 0` and look up the invoking user via `os.environ.get('SUDO_USER')`. Create all
  application files as that user using `os.chown()` or by dropping privileges with `os.seteuid()`.

**Warning signs:**
`ls -la ~/bitnest/` shows `root root` ownership; `docker compose up` fails with PostgreSQL
`initdb: error: could not change permissions of directory`.

**Phase to address:**
Installer scaffolding / project directory creation, all platform installers.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hardcode `docker-compose` (hyphen) command | Works on older systems | Breaks on any system with only V2 installed — the default since 2023 | Never — always detect dynamically |
| Write `~/` or relative paths to compose.yaml | Shorter, human-readable | Runtime failures on Docker; breaks on WSL2 path resolution | Never — always expand to absolute before writing |
| Generate password with `random` module | Trivial one-liner | Not cryptographically secure; secrets guessable | Never for secrets |
| Skip port conflict pre-flight | Faster install flow | Cryptic Docker error mid-startup that confuses first-time users | Never |
| Run entire installer as root via `sudo python3 install.py` | Avoids all permission prompts | Files owned by root; DB container cannot write to data dir | Never as design; acceptable only if `SUDO_USER` chown is applied |
| Use `depends_on` without healthcheck condition | Simpler compose.yaml | API race-conditions DB on every first-run or restart | Never for production compose templates |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Python installer → Docker install (apt) | Run all steps as sudo, including file creation | Use sudo only for apt/usermod; create app files as the real user |
| Python installer → .env generation | Use `random.choice(string.printable)` for passwords | Use `secrets.token_hex(32)` — hex only, no interpolation hazards |
| Python installer → compose.yaml generation | Write paths as user typed them (with `~`) | Expand with `os.path.expanduser` + `os.path.abspath` before writing |
| compose.yaml → PostgreSQL healthcheck | Use `depends_on` name only (no condition) | Add `pg_isready` healthcheck + `condition: service_healthy` |
| WSL2 installer → Docker detection | Conflate "daemon not running" with "not installed" | Check `/proc/version` for WSL, inspect socket error type before deciding |
| ARM64 installer → Docker APT repo | Paste x86_64 repo line with `arch=amd64` | Read `platform.machine()`, set `arch=arm64` for `aarch64` |
| Docker group membership → same session | Call `newgrp docker` via subprocess | Use `os.execvp` re-exec pattern or sudo for that session |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Using `random` module for secrets | Secrets are predictable; brute-forceable | Use `secrets.token_hex(32)` exclusively |
| Writing `.env` with `chmod 644` (world-readable) | Any local user or process can read DB password and JWT secret | Write with `chmod 600` via `os.chmod(path, 0o600)` immediately after creation |
| Storing generated secrets in install log / stdout | Secrets captured in shell history or log files | Never print generated secrets; write directly to `.env` only |
| docker group treated as equivalent to unprivileged | Users in docker group have effective root via `docker run -v /:/mnt` | Document the security implication clearly in the installer output |
| Running with `shell=True` in subprocess for user-supplied input | Shell injection via crafted directory name or username | Always pass commands as lists, never as a shell string with user input interpolated |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Installer exits after Docker install saying "please log out and log in again, then re-run" | First-time user reboots, forgets to re-run, thinks install is complete | Continue install in sudo-docker mode with a clear "re-login note" at the end; or use re-exec pattern |
| Long docker compose pull with no progress indicator | User thinks the script is frozen; Ctrl-C aborts | Run `docker compose pull` without `-d`, let Docker's own progress bars print to stdout |
| Config wizard asks 10 questions before any validation | User reaches the last question, port conflict discovered, must start over | Run port checks and dependency checks before the wizard begins |
| Confusing error on first `docker compose up` when DB not ready | User assumes their install failed | Pre-emit "Waiting for database to become ready..." message; or use healthcheck so compose waits |
| Installer always requires internet (pulls images) | Fails on air-gapped or metered connections without warning | Pre-flight: check Docker Hub reachability, estimate image sizes, warn before pulling |

---

## "Looks Done But Isn't" Checklist

- [ ] **Docker group activation:** installer works end-to-end without requiring manual logout mid-run
- [ ] **compose.yaml paths:** all volume paths are absolute (no `~`, no `./`) when written to file
- [ ] **.env passwords:** `docker compose config` shows full password value without truncation or mangling
- [ ] **DB healthcheck:** `docker compose config` shows `condition: service_healthy` on API's depends_on
- [ ] **Port pre-flight:** installer rejects or re-prompts if any required port is already bound
- [ ] **ARM64 Docker repo:** apt source line contains `arch=arm64` on aarch64 hosts
- [ ] **Multi-arch images:** `docker manifest inspect <image>:latest` confirms `linux/arm64` entry exists
- [ ] **WSL2 volumes:** no data paths under `/mnt/c/` or `/mnt/d/` in generated compose.yaml
- [ ] **.env permissions:** `stat -c %a .env` returns `600` after installer runs
- [ ] **Root install:** files in BitNest directory are NOT owned by root after install

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Docker group not active, containers failed | LOW | `sudo docker compose up -d` to start; user logs out/in; re-run without sudo |
| .env password mangled by interpolation | LOW | Re-run config wizard step or manually edit .env; `docker compose down && docker compose up -d` |
| Wrong arch in Docker APT repo | MEDIUM | Remove `/etc/apt/sources.list.d/docker.list`, correct arch, `apt update && apt install docker-ce` |
| Volume paths under /mnt/c/ on WSL2 | HIGH | Stop containers, move data to Linux path, update compose.yaml volumes, restart |
| Files owned by root after root install | MEDIUM | `sudo chown -R $USER:$USER ~/bitnest/`; verify PostgreSQL data dir ownership is fixed |
| ARM64 image missing from Docker Hub | HIGH | Must rebuild and push multi-arch images from CI; no user-side workaround |
| API race-conditioned DB (no healthcheck) | LOW | Add healthcheck + condition to compose.yaml; `docker compose down && docker compose up -d` |
| Port conflict on 5432 | LOW-MEDIUM | Stop conflicting service or change BitNest port in .env; `docker compose up -d` |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Docker group not active in same session | Linux x86_64 installer core / re-exec pattern | End-to-end install without manual logout succeeds |
| Docker Compose V1 hyphen form used | Docker detection step, all installers | `docker compose version` detection; no `docker-compose` calls in codebase |
| Special chars in .env breaking interpolation | Config wizard / secret generation, all installers | `docker compose config` shows unmangled passwords |
| Tilde / relative paths in compose.yaml | compose.yaml generation step, all installers | `grep '~' compose.yaml` returns nothing; all paths absolute |
| WSL2 Docker Desktop not running vs not installed | WSL2 installer platform detection | Correct guidance given when socket absent vs binary absent |
| WSL2 systemd absent | WSL2 installer daemon start step | Installer handles systemd-absent gracefully |
| WSL2 /mnt/c/ volume paths | WSL2 config wizard path selection | Path validator warns/rejects /mnt/ paths |
| ARM64 wrong apt arch | ARM64 Docker install step | `arch=arm64` verified in apt source line on aarch64 host |
| ARM64 image missing | ARM64 image pull verification step + CI | `docker manifest inspect` confirms arm64 entry |
| DB/API startup race condition | compose.yaml template generation | DB healthcheck + condition present in generated compose.yaml |
| Port conflict | Pre-flight checks, all installers (before wizard) | Port check runs before first user prompt |
| Root install → wrong file ownership | Installer scaffolding / project creation | BitNest directory ownership matches invoking user |

---

## Sources

- Docker post-installation steps: https://docs.docker.com/engine/install/linux-postinstall/
- Docker Compose V1 deprecation: https://www.docker.com/blog/new-docker-compose-v2-and-v1-deprecation/
- Docker Compose interpolation reference: https://docs.docker.com/reference/compose-file/interpolation/
- Compose .env special characters issue: https://github.com/docker/compose/issues/5980
- Compose tilde path issue: https://github.com/docker/compose/issues/6506
- Docker Compose startup ordering: https://docs.docker.com/compose/how-tos/startup-order/
- Docker Desktop WSL2 backend: https://docs.docker.com/desktop/features/wsl/
- Docker install on Raspberry Pi OS (official): https://docs.docker.com/engine/install/raspberry-pi-os/
- ARM64 manifest mismatch community thread: https://forums.docker.com/t/error-when-running-mysql-on-raspberry-arm-error-no-matching-manifest-for-linux-arm64-v8-in-the-manifest-list-entries/128255
- Python secrets module (official): https://docs.python.org/3/library/secrets.html
- PEP 506 — secrets module rationale: https://peps.python.org/pep-0506/
- Docker rootless mode: https://docs.docker.com/engine/security/rootless/
- WSL2 volume performance: https://docs.docker.com/desktop/features/wsl/

---
*Pitfalls research for: Python Docker Compose installer scripts (Linux x86_64, ARM64, Windows WSL2)*
*Researched: 2026-03-26*

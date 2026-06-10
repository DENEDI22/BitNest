# Stack Research

**Domain:** Python stdlib-only installer scripts for a Docker Compose self-hosted app (BitNest v0.1.0)
**Researched:** 2026-03-26
**Confidence:** HIGH (official Docker docs fetched; Python stdlib verified against docs.python.org)

---

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Python | 3.8+ | Installer runtime | stdlib-only constraint; 3.8 is the oldest Python still present on Debian Buster and later; all required modules available |
| `subprocess` (stdlib) | built-in | Run docker, docker compose, systemctl, usermod, apt/dnf/pacman | Only sane way to invoke system commands without pip; `subprocess.run(..., check=True)` raises on non-zero exit |
| `secrets` (stdlib) | Python 3.6+ | Generate DB password and JWT signing key | Cryptographically secure via `os.urandom`; `secrets.token_hex(32)` produces a 64-char hex string suitable for both |
| `shutil` (stdlib) | built-in | `shutil.which()` for binary detection; file copy | `shutil.which("docker")` checks PATH correctly; more portable than hardcoded `/usr/bin/docker` |
| `pathlib` (stdlib) | Python 3.4+ | Install dir creation, .env path, compose.yaml path | `Path.mkdir(parents=True, exist_ok=True)` is idempotent; cleaner than `os.path` |
| `sys` (stdlib) | built-in | `sys.exit()`, `sys.platform`, `sys.version_info` | Platform guard at startup; abort on wrong Python version |
| `os` (stdlib) | built-in | `os.getuid()` / `os.geteuid()` for root check; `os.environ` | Detect if running as root (uid == 0 means root, which should be warned against) |
| `platform` (stdlib) | built-in | `platform.machine()` for CPU arch | Returns `x86_64` or `aarch64`; drives distro-specific install logic |
| `json` (stdlib) | built-in | Parse `docker info --format json` output | Daemon readiness check via JSON field extraction |

### Supporting Modules (stdlib, no pip)

| Module | Purpose | When to Use |
|--------|---------|-------------|
| `getpass` | `getpass.getpass()` for masked password input | If wizard ever prompts for an existing secret — NOT for generated secrets |
| `re` | Validate user inputs (port numbers, directory names) | `re.match(r'^\d{1,5}$', val)` for port; `re.match(r'^[/~]', val)` for path |
| `textwrap` | `textwrap.dedent()` on multi-line instructions | Keeps long string literals readable in source |
| `time` | `time.sleep()` in Docker daemon wait loop | Brief sleep while waiting for socket to become ready after install |
| `threading` | Optional animated spinner during long operations | `threading.Thread(target=spinner, daemon=True)` so it dies with main process |
| `urllib.request` | Download get.docker.com fallback script | Only when no known package manager is found and `curl` is absent |
| `shlex` | Safe subprocess argument construction | `shlex.split()` when building commands that include user-supplied strings |
| `configparser` | Alternative for parsing `/etc/os-release` | Can parse it as INI; simpler than manual line splitting |

### Terminal Output (stdlib, no pip)

| Approach | Implementation | Notes |
|----------|---------------|-------|
| ANSI escape codes | `"\033[32mOK\033[0m"` for green; `"\033[31mERROR\033[0m"` for red | Works on all modern Linux terminals; check `sys.stdout.isatty()` and suppress codes when output is piped |
| Flushed print | `print("Installing...", flush=True)` | Ensures progress lines appear before subprocess blocks |
| Colored prompt workaround | Print colored label with `print()`, then call `input("  Enter value: ")` with plain text only | Known CPython/readline bug: ANSI codes in the `input()` prompt string cause readline to miscalculate cursor position, breaking backspace and arrow keys |

---

## Docker Engine Install Commands by Distro

### Debian / Ubuntu (apt)

Raspberry Pi OS Bookworm ARM64 follows the **Debian** path (`ID=debian` in `/etc/os-release`), not Ubuntu.

```bash
# 1. Prerequisites
sudo apt update
sudo apt install -y ca-certificates curl

# 2. GPG key (Debian)
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
# For Ubuntu: replace /debian/ with /ubuntu/ in URL above
sudo chmod a+r /etc/apt/keyrings/docker.asc

# 3. Repository (Debian — Bookworm example)
echo "Types: deb
URIs: https://download.docker.com/linux/debian
Suites: bookworm
Components: stable
Signed-By: /etc/apt/keyrings/docker.asc" | sudo tee /etc/apt/sources.list.d/docker.sources
# For Ubuntu: change URIs to /ubuntu and use UBUNTU_CODENAME or VERSION_CODENAME from /etc/os-release

sudo apt update

# 4. Install packages
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Docker daemon **starts and enables itself automatically** on Debian/Ubuntu. No explicit `systemctl enable --now docker` is required, but it is safe to call.

### Fedora (dnf)

```bash
# 1. Add Docker repo
sudo dnf -y install dnf-plugins-core
sudo dnf config-manager --add-repo https://download.docker.com/linux/fedora/docker-ce.repo

# 2. Install packages
sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# 3. Enable daemon (NOT auto-started on Fedora)
sudo systemctl enable --now docker
```

### RHEL / CentOS / Rocky Linux / AlmaLinux (dnf)

```bash
# 1. Add Docker repo
sudo dnf -y install dnf-plugins-core
sudo dnf config-manager --add-repo https://download.docker.com/linux/rhel/docker-ce.repo
# For CentOS / Rocky / AlmaLinux: replace /rhel/ with /centos/

# 2. Install packages
sudo dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# 3. Enable daemon (NOT auto-started on RHEL/CentOS)
sudo systemctl enable --now docker
```

### Arch Linux (pacman)

Docker is in the official Arch community repo — no third-party repo needed.

```bash
sudo pacman -Syu --noconfirm docker docker-compose

# Enable daemon (NOT auto-started on Arch)
sudo systemctl enable --now docker
```

Note: on Arch, `docker-compose` installs the V2 plugin binary at `/usr/lib/docker/cli-plugins/docker-compose` and provides a `/usr/bin/docker-compose` symlink. Both `docker compose` and `docker-compose` work.

### Fallback: get.docker.com convenience script

Use when `/etc/os-release` `ID` is not recognised (Gentoo, Void, NixOS, etc.).

```bash
curl -fsSL https://get.docker.com -o /tmp/get-docker.sh
sudo sh /tmp/get-docker.sh
```

**Caveats:** installs latest stable Docker CE; may trigger unexpected major-version upgrades on subsequent calls; not recommended for production servers. The installer should print a warning before using this fallback and prompt the user to confirm.

---

## Distro Detection Pattern

Read `/etc/os-release` to drive install path selection:

```python
import pathlib

def read_os_release() -> dict:
    result = {}
    try:
        for line in pathlib.Path("/etc/os-release").read_text().splitlines():
            line = line.strip()
            if "=" in line and not line.startswith("#"):
                k, _, v = line.partition("=")
                result[k] = v.strip('"').strip("'")
    except OSError:
        pass
    return result

info = read_os_release()
distro_id   = info.get("ID", "").lower()          # debian, ubuntu, fedora, rhel, arch
id_like     = info.get("ID_LIKE", "").lower()      # "rhel fedora" for CentOS/Rocky
codename    = info.get("VERSION_CODENAME", info.get("UBUNTU_CODENAME", ""))
```

Decision tree:

- `distro_id == "arch"` → pacman path
- `distro_id == "ubuntu" or "ubuntu" in id_like` → apt path with ubuntu URL
- `distro_id in ("debian", "raspbian") or "debian" in id_like` → apt path with debian URL
- `distro_id == "fedora"` → dnf path with fedora URL
- `distro_id in ("rhel", "centos", "rocky", "almalinux") or "rhel" in id_like` → dnf path with rhel/centos URL
- No match → get.docker.com fallback (with user confirmation)

---

## Docker Compose V2 Plugin Detection

Compose V2 (Go-based Docker CLI plugin) is invoked as `docker compose` (space).
Compose V1 (Python standalone binary `docker-compose` with hyphen) is EOL since December 2023 and is no longer in Docker's official repos.

Detection order in the installer:

```python
import subprocess, shutil

def find_compose_cmd() -> list | None:
    # V2 plugin — preferred, installed via docker-compose-plugin package
    try:
        r = subprocess.run(
            ["docker", "compose", "version"],
            capture_output=True, timeout=10
        )
        if r.returncode == 0:
            return ["docker", "compose"]
    except FileNotFoundError:
        pass

    # V1 standalone — EOL fallback; warn user
    if shutil.which("docker-compose"):
        return ["docker-compose"]   # caller should log a deprecation warning

    return None  # neither present
```

When `docker-compose-plugin` is installed via the apt/dnf/pacman commands above, `["docker", "compose"]` always succeeds and the V1 branch is never reached.

---

## Docker Group Handling

Adding a user to the `docker` group grants root-equivalent host access (full Docker daemon control = root). The installer must print a security note.

```bash
# Create group if absent (groupadd exits 9 if group already exists — treat as success)
sudo groupadd docker 2>/dev/null || true

# Add current user to docker group
sudo usermod -aG docker "$USER"
```

### Session Reload Problem

`usermod` changes take effect only when the user opens a new login session. The installer **must not** call `newgrp docker` — `newgrp` forks a new shell and suspends the calling process inside it, hanging the script.

Recommended installer approach:

1. Call `sudo usermod -aG docker $USER`.
2. Print: "Docker group membership requires a new session. Log out and log back in, or reboot."
3. For the current install run, invoke docker commands with `sudo` explicitly, e.g. `sudo docker compose up -d`.
4. Tell the user that future sessions will not need sudo.

---

## WSL2 Detection and Docker Desktop Guidance

### Detecting WSL2 from inside a WSL2 shell

```python
import pathlib

def is_wsl2() -> bool:
    try:
        osrelease = pathlib.Path("/proc/sys/kernel/osrelease").read_text()
        return "WSL2" in osrelease
    except OSError:
        return False
```

Fallback using `/proc/version` (covers both WSL1 and WSL2):

```python
def is_wsl_any() -> bool:
    try:
        content = pathlib.Path("/proc/version").read_text().lower()
        return "microsoft" in content
    except OSError:
        return False
```

### Detecting Docker Desktop daemon reachability in WSL2

Docker Desktop bridges its Unix socket into WSL2 distros at `/var/run/docker.sock`. The check:

```python
import subprocess

def docker_daemon_reachable() -> bool:
    try:
        r = subprocess.run(
            ["docker", "info", "--format", "{{.ServerVersion}}"],
            capture_output=True, timeout=10
        )
        return r.returncode == 0
    except FileNotFoundError:
        return False
```

### WSL2 installer flow

1. Detect WSL2 via `/proc/sys/kernel/osrelease`.
2. Call `docker_daemon_reachable()`.
3. **Reachable:** Docker Desktop is running and WSL integration is active — proceed to config wizard.
4. **Not reachable:** Print setup instructions (see below); retry loop up to 3 times with 10-second sleep after user presses Enter.
5. The WSL2 installer does **not** attempt to install Docker Engine — Docker Desktop is the supported and expected path for Windows users.

Setup message when daemon not reachable:

```
Docker Desktop is not running or WSL integration is not enabled.

Steps:
  1. Install Docker Desktop for Windows:
     https://docs.docker.com/desktop/install/windows-install/
  2. Open Docker Desktop and wait for it to start (whale icon in taskbar).
  3. Go to Settings > Resources > WSL Integration.
  4. Enable integration for your WSL2 distro, then click Apply & Restart.
  5. Press Enter here to re-check.
```

---

## Raspberry Pi OS Bookworm ARM64 Notes

| Item | Detail |
|------|--------|
| Architecture | `aarch64` — fully supported; `platform.machine()` returns `aarch64` |
| Distro path | Follow Debian install path; `ID=debian` in `/etc/os-release` |
| `VERSION_CODENAME` | `bookworm` — confirmed from `/etc/os-release` |
| Docker repo URL | `https://download.docker.com/linux/debian` with `bookworm stable` |
| Multi-arch packages | Same Docker repo URL — arm64 packages served automatically |
| `docker-compose-plugin` | Available for arm64 in same repo — no special handling needed |
| Daemon autostart | Starts automatically after install (Debian systemd behaviour) |
| RAM advisory | Pi 4/5 with 4 GB+ is comfortable; 2 GB is borderline; 1 GB will likely OOM under load |
| Swap recommendation | Read `/proc/meminfo` `MemTotal`; if under 2 GB, print advisory to enable 1–2 GB swap |

Pi detection in installer:

```python
import platform
arch = platform.machine()   # "aarch64" for Pi 4/5 64-bit OS, "armv7l" for 32-bit
```

The ARM64 installer should work identically to the x86_64 installer once the Debian apt path is selected — there is no Pi-specific Docker package.

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| `secrets.token_hex(32)` | `os.urandom(32).hex()` | Functionally equivalent — `secrets` is just a clean wrapper; either is fine |
| `subprocess.run(..., check=True)` | `os.system()` | Never — `os.system` does not capture output or provide error handling |
| ANSI codes printed before `input()` | ANSI codes embedded in `input("...")` prompt string | Avoid codes in prompt — readline cursor-position bug breaks line editing |
| `shutil.which("docker")` | `pathlib.Path("/usr/bin/docker").exists()` | `shutil.which` respects PATH — more portable; hardcoded paths miss snap or custom installs |
| Debian apt path for Raspberry Pi | Ubuntu apt path | `raspios` reports `ID=debian` — the Ubuntu repo URL would fail on Raspberry Pi OS |
| `["docker", "compose"]` V2 | `["docker-compose"]` V1 | V1 EOL December 2023; no longer in Docker's official repos |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `random.token_hex()` or `random.choice()` for secrets | `random` is not cryptographically secure — predictable seed | `secrets.token_hex(32)` |
| `os.system("sudo apt install ...")` | No output capture, no returncode, no error propagation | `subprocess.run(["sudo", "apt", "install", ...], check=True)` |
| ANSI codes inside `input()` prompt argument | readline cursor-position miscalculation — breaks backspace and arrows | `print()` the colored label, then `input("  plain prompt: ")` |
| `docker-compose` (hyphen) as primary command | V1 is EOL December 2023; absent from Docker's repos | `docker compose` (V2 plugin) |
| `pip install` for any installer dependency | Violates stdlib-only constraint; pip may not be present or may install to wrong env | Use only stdlib modules |
| `newgrp docker` inside installer script | Forks a subshell; suspends and hangs the installer | Print logout-and-back-in instruction; use `sudo docker ...` for current session |

---

## Stack Patterns by Variant

**Linux x86_64 installer (`installers/linux-x86_64/install.py`):**
- Read `/etc/os-release`, select apt/dnf/pacman/fallback path based on `ID` and `ID_LIKE`
- Install Docker Engine via selected path
- Call `systemctl enable --now docker` (safe on all distros; Debian ignores it gracefully if already started)
- Add user to docker group + print re-login warning
- Run config wizard: install dir, port, generate DB password + JWT key, write `.env` and `compose.yaml`
- Run `sudo docker compose up -d` for first install (uses sudo because group change not yet active)

**Linux ARM64 installer (`installers/linux-arm64/install.py`):**
- Same flow as x86_64
- Enforce Debian apt path when `distro_id in ("debian", "raspbian")` (covers Raspberry Pi OS)
- Check `MemTotal` in `/proc/meminfo`; if under 2 GB, print swap advisory
- No Pi-specific Docker packages — standard Debian arm64 packages work

**Windows WSL2 installer (`installers/windows-wsl2/install.py`):**
- Verify WSL2 via `/proc/sys/kernel/osrelease`
- Do NOT install Docker Engine — guide user to Docker Desktop for Windows
- Check daemon reachability via `docker info`; retry loop with user confirmation
- Proceed to wizard once Docker is confirmed reachable

---

## Version Compatibility

| Item | Minimum | Notes |
|------|---------|-------|
| Python | 3.8 | `secrets` since 3.6; `subprocess.run` since 3.5; `pathlib` since 3.4; f-strings since 3.6 |
| Docker Engine | 20.10 | Compose V2 plugin first available as separate install; bundled by default in 23.0+ |
| `docker-compose-plugin` | any version from Docker's repo | Installed as part of the apt/dnf/pacman command sequences above |
| Debian Bookworm | 12 | `VERSION_CODENAME=bookworm` used in repo `Suites` line |
| Raspberry Pi OS | Bookworm (2023+) | Earlier releases (Bullseye/Buster) work but have different `VERSION_CODENAME` values |
| Docker Desktop (WSL2) | 4.x+ | WSL2 backend and WSL Integration feature available since Docker Desktop 4.0 |

---

## Sources

- [Install Docker Engine on Ubuntu — docs.docker.com](https://docs.docker.com/engine/install/ubuntu/) — apt GPG key setup, repository setup, package names (HIGH — official docs, fetched 2026-03-26)
- [Install Docker Engine on Debian — docs.docker.com](https://docs.docker.com/engine/install/debian/) — Debian-specific apt commands, arm64 support confirmed (HIGH — official docs, fetched 2026-03-26)
- [Install Docker Engine on Fedora — docs.docker.com](https://docs.docker.com/engine/install/fedora/) — dnf config-manager commands (HIGH — official docs, fetched 2026-03-26)
- [Install Docker Engine on RHEL — docs.docker.com](https://docs.docker.com/engine/install/rhel/) — RHEL-specific dnf repo URL (HIGH — official docs, fetched 2026-03-26)
- [Install Docker Engine on Raspberry Pi OS — docs.docker.com](https://docs.docker.com/engine/install/raspberry-pi-os/) — confirms arm64 uses Debian path (HIGH — official docs)
- [Docker Engine Linux post-install — docs.docker.com](https://docs.docker.com/engine/install/linux-postinstall/) — docker group usermod commands, newgrp behaviour, security warning (HIGH — official docs, fetched 2026-03-26)
- [Docker Desktop WSL2 integration — docs.docker.com](https://docs.docker.com/desktop/features/wsl/) — socket bridge mechanism, CLI detection approach (HIGH — official docs, fetched 2026-03-26)
- [docker/docker-install — github.com](https://github.com/docker/docker-install) — get.docker.com convenience script caveats (HIGH — official Docker repo)
- [Docker on Arch Linux — oneuptime.com blog + ArchWiki](https://wiki.archlinux.org/title/Docker) — pacman package name, systemctl required (MEDIUM — community docs corroborated by WebSearch)
- [Python secrets module — docs.python.org](https://docs.python.org/3/library/secrets.html) — `token_hex`, security recommendation for 32 bytes (HIGH — official docs)
- [readline ANSI prompt bug — bugs.python.org issue 17337](https://bugs.python.org/issue17337) — confirmed readline cursor miscalculation when ANSI codes appear in `input()` prompt (HIGH — CPython issue tracker)
- [WSL2 detection via /proc — scivision.dev](https://www.scivision.dev/python-detect-wsl/) — `/proc/sys/kernel/osrelease` WSL2 check pattern (MEDIUM — single source, community article)
- [Compose V2 GA announcement — docker.com](https://www.docker.com/blog/announcing-compose-v2-general-availability/) — V1 EOL date December 2023, V2 plugin architecture (HIGH — official Docker blog)

---

*Stack research for: Python stdlib installer scripts — BitNest v0.1.0 Distribution & Installer milestone*
*Researched: 2026-03-26*

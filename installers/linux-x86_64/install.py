#!/usr/bin/env python3
"""
BitNest Linux x86_64 Installer
Guides a user from a bare Linux machine to a running BitNest Docker Compose stack.
"""

from __future__ import annotations

import json
import os
import secrets
import shutil
import socket
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path


# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------

def validate_port(value: str) -> tuple[bool, str]:
    """Return (True, '') if value is a valid port (1024–65535), else (False, msg)."""
    try:
        port = int(value)
    except (ValueError, TypeError):
        return False, "Enter a port number between 1024 and 65535"
    if 1024 <= port <= 65535:
        return True, ""
    return False, "Enter a port number between 1024 and 65535"


def validate_path(value: str) -> tuple[bool, str]:
    """Return (True, resolved_abs_path) or (False, error_msg)."""
    if not value or not value.strip():
        return False, "Enter a valid path (e.g. ~/bitnest)"
    try:
        resolved = Path(value).expanduser().resolve()
        return True, str(resolved)
    except Exception:
        return False, "Enter a valid path (e.g. ~/bitnest)"


def validate_username(value: str) -> tuple[bool, str]:
    """Return (True, '') if username is at least 3 chars, else (False, msg)."""
    if len(value) >= 3:
        return True, ""
    return False, "Username must be at least 3 characters"


def validate_password(value: str) -> tuple[bool, str]:
    """Return (True, '') if password is at least 8 chars, else (False, msg)."""
    if len(value) >= 8:
        return True, ""
    return False, "Password must be at least 8 characters"


# ---------------------------------------------------------------------------
# Preflight checks
# ---------------------------------------------------------------------------

def is_port_free(port: int) -> bool:
    """Return True if the port is available for binding, False if already in use."""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 0)
        try:
            s.bind(("0.0.0.0", port))
            return True
        except OSError:
            return False


def check_disk_space(path: str = "/", min_gb: float = 5.0) -> tuple[bool, float]:
    """Return (has_enough, free_gb). True if free space >= min_gb."""
    usage = shutil.disk_usage(path)
    free_gb = usage.free / (1024 ** 3)
    return free_gb >= min_gb, free_gb


def check_docker() -> tuple[bool, bool]:
    """Return (docker_installed, compose_v2_available)."""
    docker_path = shutil.which("docker")
    if not docker_path:
        return False, False
    try:
        result = subprocess.run(
            ["docker", "compose", "version"],
            capture_output=True,
            text=True,
        )
        compose_v2 = result.returncode == 0
    except Exception:
        compose_v2 = False
    return True, compose_v2


def check_compose_version() -> str:
    """Return the output of `docker compose version`."""
    try:
        result = subprocess.run(
            ["docker", "compose", "version"],
            capture_output=True,
            text=True,
            check=True,
        )
        return result.stdout.strip()
    except Exception:
        return ""


# ---------------------------------------------------------------------------
# Secret generation
# ---------------------------------------------------------------------------

def generate_secret() -> str:
    """Return a cryptographically secure 64-char hex string."""
    return secrets.token_hex(32)


# ---------------------------------------------------------------------------
# Compose template
# ---------------------------------------------------------------------------

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


def render_compose(
    api_port: int,
    frontend_port: int,
    docker_hub_user: str = "denedi22",
) -> str:
    """Render the compose template with given ports and Docker Hub user."""
    return COMPOSE_TEMPLATE.format(
        docker_hub_user=docker_hub_user,
        api_port=api_port,
        frontend_port=frontend_port,
    )


# ---------------------------------------------------------------------------
# .env writer
# ---------------------------------------------------------------------------

def write_env_file(env_path: Path, cfg: dict) -> None:
    """Write all runtime config to .env and chmod 600 immediately."""
    lines = [
        "# BitNest configuration — generated by installer",
        f"DATA_DIR={cfg['install_dir']}",
        f"POSTGRES_PASSWORD={cfg['db_password']}",
        f"AUTH_SIGNING_KEY={cfg['jwt_key']}",
        f"BITNEST_ADMIN_USER={cfg['admin_user']}",
        f"BITNEST_ADMIN_PASS={cfg['admin_pass']}",
    ]
    env_path.write_text("\n".join(lines) + "\n")
    os.chmod(str(env_path), 0o600)


# ---------------------------------------------------------------------------
# Filesystem
# ---------------------------------------------------------------------------

def create_install_dirs(install_dir: Path) -> None:
    """Create install_dir/data/storage and install_dir/data/postgres."""
    (install_dir / "data" / "storage").mkdir(parents=True, exist_ok=True)
    (install_dir / "data" / "postgres").mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------------------
# State file
# ---------------------------------------------------------------------------

def state_path() -> Path:
    """Return the XDG-compliant path to the BitNest install state file."""
    base = Path(os.environ.get("XDG_CONFIG_HOME") or (Path.home() / ".config"))
    return base / "bitnest" / "install.json"


def write_state(
    install_dir: str,
    api_port: int,
    frontend_port: int,
    compose_file: str,
) -> None:
    """Write install state to the XDG config file."""
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
    """Read install state from XDG config file. Returns None if not found."""
    p = state_path()
    return json.loads(p.read_text()) if p.exists() else None


# ---------------------------------------------------------------------------
# Distro detection
# ---------------------------------------------------------------------------

def read_os_release() -> dict:
    """Parse /etc/os-release into a dict. Returns {} on error."""
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
    """Return the Docker install path key for the given /etc/os-release info dict."""
    distro_id = info.get("ID", "").lower()
    id_like = info.get("ID_LIKE", "").lower()
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
    return "fallback"


# ---------------------------------------------------------------------------
# Docker install commands
# ---------------------------------------------------------------------------

def get_docker_install_commands(path: str) -> list[list[str]]:
    """Return a list of command lists to install Docker for the given distro path."""
    if path in ("apt_ubuntu", "apt_debian"):
        return [
            ["sudo", "apt-get", "update"],
            ["sudo", "apt-get", "install", "-y", "ca-certificates", "curl"],
            ["sudo", "install", "-m", "0755", "-d", "/etc/apt/keyrings"],
            [
                "sudo", "curl", "-fsSL",
                "https://download.docker.com/linux/ubuntu/gpg",
                "-o", "/etc/apt/keyrings/docker.asc",
            ],
            ["sudo", "chmod", "a+r", "/etc/apt/keyrings/docker.asc"],
            # apt source add is done via a shell command — represented as list for run_cmd
            [
                "sudo", "bash", "-c",
                'echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] '
                'https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" '
                '| sudo tee /etc/apt/sources.list.d/docker.list > /dev/null',
            ],
            ["sudo", "apt-get", "update"],
            [
                "sudo", "apt-get", "install", "-y",
                "docker-ce", "docker-ce-cli", "containerd.io", "docker-compose-plugin",
            ],
        ]
    if path == "dnf_fedora":
        return [
            ["sudo", "dnf", "-y", "install", "dnf-plugins-core"],
            [
                "sudo", "dnf-3", "config-manager", "--add-repo",
                "https://download.docker.com/linux/fedora/docker-ce.repo",
            ],
            [
                "sudo", "dnf", "install", "-y",
                "docker-ce", "docker-ce-cli", "containerd.io", "docker-compose-plugin",
            ],
            ["sudo", "systemctl", "start", "docker"],
            ["sudo", "systemctl", "enable", "docker"],
        ]
    if path == "dnf_rhel":
        return [
            ["sudo", "dnf", "-y", "install", "dnf-plugins-core"],
            [
                "sudo", "dnf", "config-manager", "--add-repo",
                "https://download.docker.com/linux/centos/docker-ce.repo",
            ],
            [
                "sudo", "dnf", "install", "-y",
                "docker-ce", "docker-ce-cli", "containerd.io", "docker-compose-plugin",
            ],
            ["sudo", "systemctl", "start", "docker"],
            ["sudo", "systemctl", "enable", "docker"],
        ]
    if path == "pacman":
        return [
            ["sudo", "pacman", "-Sy", "--noconfirm", "docker", "docker-compose"],
            ["sudo", "systemctl", "start", "docker"],
            ["sudo", "systemctl", "enable", "docker"],
        ]
    # fallback: get.docker.com convenience script
    return [
        ["curl", "-fsSL", "https://get.docker.com", "-o", "/tmp/get-docker.sh"],
        ["sudo", "sh", "/tmp/get-docker.sh"],
    ]


# ---------------------------------------------------------------------------
# Health polling
# ---------------------------------------------------------------------------

def parse_compose_ps_json(stdout: str) -> dict[str, bool]:
    """Parse `docker compose ps --format json` output (one JSON object per line).

    Returns per-service health: True if healthy/running-no-healthcheck, False otherwise.
    """
    result: dict[str, bool] = {}
    for line in stdout.splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            service = json.loads(line)
        except json.JSONDecodeError:
            continue
        name = service.get("Name", "")
        state = service.get("State", "").lower()
        health = service.get("Health", "").lower()
        if health == "healthy":
            result[name] = True
        elif state == "running" and health == "":
            result[name] = True
        else:
            result[name] = False
    return result


def poll_health(
    compose_file: str,
    timeout: int = 60,
    use_sudo: bool = False,
) -> dict[str, bool]:
    """Poll `docker compose ps` until all services healthy or timeout reached.

    Returns per-service status dict. Never raises — returns last known state on timeout.
    """
    prefix = ["sudo"] if use_sudo else []
    cmd = prefix + ["docker", "compose", "-f", compose_file, "ps", "--format", "json"]
    deadline = time.time() + timeout
    status: dict[str, bool] = {}
    while time.time() < deadline:
        try:
            result = run_cmd(cmd, check=False, capture=True)
            status = parse_compose_ps_json(result.stdout or "")
            if status and all(status.values()):
                return status
        except Exception:
            pass
        time.sleep(2)
    return status


# ---------------------------------------------------------------------------
# Subprocess helper
# ---------------------------------------------------------------------------

def run_cmd(
    cmd: list[str],
    check: bool = True,
    capture: bool = False,
) -> subprocess.CompletedProcess:
    """Run a subprocess command. Never uses shell=True or cwd=.

    Args:
        cmd: Command as a list of strings.
        check: If True, raise CalledProcessError on non-zero exit.
        capture: If True, capture stdout and stderr.
    """
    kwargs: dict = {"check": check}
    if capture:
        kwargs["capture_output"] = True
        kwargs["text"] = True
    return subprocess.run(cmd, **kwargs)


# --- TUI --- #

if __name__ == "__main__":
    # Ensure textual is available before launching TUI
    try:
        import textual  # noqa: F401
    except ImportError:
        print("Installing required dependency (textual)...")
        subprocess.run(
            [sys.executable, "-m", "pip", "install", "textual>=0.89.0"],
            check=True,
        )

    # TUI entry point (implemented in Plan 02)
    print("BitNest Installer — TUI not yet implemented. Run install.py to start.")

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

def _ensure_textual() -> None:
    """Auto-install textual if missing. Called only from __main__ entry point."""
    try:
        import textual  # noqa: F401
        return
    except ImportError:
        pass

    print("Installing UI dependency (textual)...")
    pkg = "textual>=0.89.0"
    strategies = [
        [sys.executable, "-m", "pip", "install", "--quiet", "--user", pkg],
        [sys.executable, "-m", "pip", "install", "--quiet", "--break-system-packages", pkg],
    ]
    for cmd in strategies:
        result = subprocess.run(cmd, capture_output=True)
        if result.returncode == 0:
            break
    else:
        print(
            "Could not auto-install textual.\n"
            "Please install it manually:\n"
            "  pacman -S python-textual   (Arch Linux)\n"
            "  pip install --user textual  (other distros)\n"
            "  pipx install textual        (isolated install)"
        )
        sys.exit(1)

    # Always re-exec: TUI classes were defined with _Stub bases before textual
    # was available, so the whole module must be reloaded with textual present.
    os.execv(sys.executable, [sys.executable] + sys.argv)


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
# Textual imports are guarded so this file remains importable for unit tests
# even when textual is not installed. When running as __main__, _ensure_textual()
# installs textual (and re-execs if needed) before these imports are attempted.
if __name__ == "__main__":
    _ensure_textual()

try:
    from textual.app import App, ComposeResult, Screen
    from textual.binding import Binding
    from textual.widgets import Button, Footer, Input, Label, ListItem, ListView, RichLog, Static
    from textual.containers import Horizontal, Vertical
    from textual import work
    from rich.text import Text
    _TEXTUAL_AVAILABLE = True
except ImportError:
    # Stub base classes so TUI class definitions don't raise NameError at import time.
    # These stubs are never instantiated outside of __main__ (where _ensure_textual
    # guarantees a real textual install before App.run() is called).
    _TEXTUAL_AVAILABLE = False

    class App:  # type: ignore[no-redef]
        pass

    class Screen:  # type: ignore[no-redef]
        pass

    class Static:  # type: ignore[no-redef]
        pass

    def work(*args, **kwargs):  # type: ignore[no-redef]
        def _decorator(fn):
            return fn
        return _decorator if args and callable(args[0]) else _decorator

    class _Stub:  # type: ignore[no-redef]
        """Generic stub that accepts any arguments without raising."""
        def __init__(self, *args: object, **kwargs: object) -> None:
            pass

    ComposeResult = _Stub  # type: ignore[misc,assignment]
    Binding = _Stub  # type: ignore[misc,assignment]
    Button = _Stub  # type: ignore[misc,assignment]
    Footer = _Stub  # type: ignore[misc,assignment]
    Input = _Stub  # type: ignore[misc,assignment]
    Label = _Stub  # type: ignore[misc,assignment]
    ListItem = _Stub  # type: ignore[misc,assignment]
    ListView = _Stub  # type: ignore[misc,assignment]
    RichLog = _Stub  # type: ignore[misc,assignment]
    Horizontal = _Stub  # type: ignore[misc,assignment]
    Vertical = _Stub  # type: ignore[misc,assignment]

    class Text:  # type: ignore[no-redef]
        """Stub for rich.text.Text."""
        def __init__(self, text: str = "", style: str = "") -> None:
            pass

        @staticmethod
        def from_ansi(text: str) -> "Text":
            return Text(text)


# ---------------------------------------------------------------------------
# Step Indicator widget
# ---------------------------------------------------------------------------

class StepIndicator(Static):
    """Horizontal progress bar showing current wizard step."""

    def __init__(self, current_step: int = 1) -> None:
        super().__init__()
        self.current_step = current_step

    def render(self) -> str:
        steps = ["Prerequisites", "Configuration", "Installing", "Done"]
        parts = []
        for i, name in enumerate(steps, 1):
            if i == self.current_step:
                parts.append(f"[bold #e94560][ Step {i}/4 {name} ][/]")
            else:
                parts.append(f"[#636e72]  Step {i}/4 {name}  [/]")
        return "  ".join(parts)


# ---------------------------------------------------------------------------
# Main Menu Screen
# ---------------------------------------------------------------------------

class MainMenuScreen(Screen):  # type: ignore[misc]
    """Entry screen with Install / Update / Uninstall options."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def compose(self) -> ComposeResult:
        yield Label("BitNest Installer", id="title")
        yield ListView(
            ListItem(Label("Install BitNest"), id="install"),
            ListItem(Label("Update BitNest"), id="update"),
            ListItem(Label("Uninstall BitNest"), id="uninstall"),
            id="main_menu",
        )
        yield Footer()

    def on_mount(self) -> None:
        state = read_state()
        list_view = self.query_one("#main_menu", ListView)
        if state:
            list_view.index = 1  # pre-highlight Update per D-02
        else:
            list_view.index = 0  # pre-highlight Install

    def on_list_view_selected(self, event: ListView.Selected) -> None:
        item_id = event.item.id
        if item_id == "install":
            self.app.push_screen(Step1PrerequisitesScreen())
        elif item_id == "update":
            self.app.push_screen(UpdateScreen())
        elif item_id == "uninstall":
            self.app.push_screen(UninstallScreen())

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Update Screen
# ---------------------------------------------------------------------------

class UpdateScreen(Screen):  # type: ignore[misc]
    """Pull latest images and restart the BitNest stack."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(self) -> None:
        super().__init__()
        self._state: dict | None = None

    def compose(self) -> ComposeResult:
        self._state = read_state()
        yield Label("[bold]Update BitNest[/]", id="title")
        if self._state:
            yield Label(f"Installed at: {self._state.get('install_dir', '')}")
            yield Label(f"Last installed: {self._state.get('installed_at', '')}")
        else:
            yield Label("[#e17055]No existing installation found.[/]")
        yield Horizontal(
            Button("Update BitNest", id="update_btn", classes="accent"),
            Button("Back", id="back_btn", classes="dim"),
            id="update_buttons",
        )
        yield RichLog(id="update_log", highlight=True, markup=True)

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "back_btn":
            self.app.pop_screen()
        elif event.button.id == "update_btn":
            if not self._state:
                return
            # Hide Back button per UI-SPEC; show log; run update
            try:
                self.query_one("#back_btn", Button).remove()
            except Exception:
                pass
            try:
                self.query_one("#update_btn", Button).disabled = True
            except Exception:
                pass
            self._run_update()

    @work(exclusive=True, thread=True)
    def _run_update(self) -> None:
        """Pull latest images and restart stack without docker compose down."""
        state = self._state
        if not state:
            return
        log = self.query_one("#update_log", RichLog)
        compose_file = state["compose_file"]

        try:
            # Pull latest images
            self.call_from_thread(
                log.write,
                Text("[•] Pulling latest images...", style="#fdcb6e"),
            )
            cmd = ["sudo", "docker", "compose", "-f", compose_file, "pull"]
            with subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
            ) as proc:
                for line in proc.stdout:  # type: ignore[union-attr]
                    stripped = line.rstrip()
                    if stripped:
                        self.call_from_thread(
                            log.write,
                            Text(f"    → {stripped}", style="dim"),
                        )
            if proc.returncode != 0:
                self.call_from_thread(
                    log.write,
                    Text("[✗] Image pull failed. Press Q to quit.", style="#e17055"),
                )
                return
            self.call_from_thread(
                log.write,
                Text("[✔] Images pulled", style="#00b894"),
            )

            # Restart stack (no docker compose down per D-21)
            subprocess.run(
                ["sudo", "docker", "compose", "-f", compose_file, "up", "-d"],
                check=True,
            )
            self.call_from_thread(
                log.write,
                Text("[✔] Stack restarted", style="#00b894"),
            )

            # Health poll
            health = poll_health(compose_file, timeout=60, use_sudo=True)
            for svc, ok in sorted(health.items()):
                icon = "[✔]" if ok else "[✗]"
                color = "#00b894" if ok else "#e17055"
                self.call_from_thread(
                    log.write,
                    Text(
                        f"    {svc:12s} {icon} {'healthy' if ok else 'unhealthy'}",
                        style=color,
                    ),
                )

            if all(health.values()):
                self.call_from_thread(
                    log.write,
                    Text(
                        "[✔] Update complete — all services healthy",
                        style="#00b894",
                    ),
                )
            else:
                self.call_from_thread(
                    log.write,
                    Text(
                        "[✗] Some services unhealthy after update. Press Q to quit.",
                        style="#e17055",
                    ),
                )

            # Show Quit button
            self.app.call_from_thread(
                self._show_quit_button,
            )

        except (subprocess.CalledProcessError, OSError) as exc:
            self.call_from_thread(
                log.write,
                Text(f"[✗] Update failed: {exc}. Press Q to quit.", style="#e17055"),
            )

    def _show_quit_button(self) -> None:
        try:
            self.query_one("#update_buttons", Horizontal).mount(
                Button("Quit", id="quit_btn", classes="dim")
            )
        except Exception:
            pass

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Uninstall Screen (first confirm)
# ---------------------------------------------------------------------------

class UninstallScreen(Screen):  # type: ignore[misc]
    """First uninstall confirmation — stops BitNest."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(self) -> None:
        super().__init__()
        self._state: dict | None = None

    def compose(self) -> ComposeResult:
        self._state = read_state()
        yield Label("[bold]Uninstall BitNest[/]", id="title")
        if self._state:
            yield Label("This will stop BitNest and remove the installation.")
            yield Label(f"Install directory: {self._state.get('install_dir', '')}")
            yield Label("Continue?")
            yield Horizontal(
                Button("Continue to Uninstall", id="continue_btn", classes="accent"),
                Button("Back", id="back_btn", classes="dim"),
            )
        else:
            yield Label("[#e17055]No existing installation found.[/]")
            yield Horizontal(
                Button("Back", id="back_btn", classes="dim"),
            )

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "back_btn":
            self.app.pop_screen()
        elif event.button.id == "continue_btn":
            if self._state:
                self.app.push_screen(UninstallConfirm2Screen(self._state))

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Uninstall Second Confirm Screen
# ---------------------------------------------------------------------------

class UninstallConfirm2Screen(Screen):  # type: ignore[misc]
    """Second uninstall confirmation — data deletion with two-factor confirmation."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(self, state: dict) -> None:
        super().__init__()
        self._state = state

    def compose(self) -> ComposeResult:
        install_dir = self._state.get("install_dir", "")
        yield Label("[bold #e17055]⚠  Delete all data?[/]")
        yield Label(
            "[#e17055]This will permanently delete all files and the database.[/]"
        )
        yield Label("[#e17055]This cannot be undone.[/]")
        yield Label(f"[#e17055]Directory to be deleted: {install_dir}/data[/]")
        yield Horizontal(
            Button("Delete Everything", id="delete_btn", classes="destructive"),
            Button("Keep My Data", id="keep_btn", classes="dim"),
        )
        yield RichLog(id="uninstall_log", highlight=True, markup=True)

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "delete_btn":
            try:
                self.query_one("#delete_btn", Button).disabled = True
                self.query_one("#keep_btn", Button).disabled = True
            except Exception:
                pass
            self._run_uninstall(delete_data=True)
        elif event.button.id == "keep_btn":
            try:
                self.query_one("#delete_btn", Button).disabled = True
                self.query_one("#keep_btn", Button).disabled = True
            except Exception:
                pass
            self._run_uninstall(delete_data=False)

    @work(exclusive=True, thread=True)
    def _run_uninstall(self, delete_data: bool) -> None:
        """Stop the stack and optionally delete data."""
        state = self._state
        log = self.query_one("#uninstall_log", RichLog)
        compose_file = state["compose_file"]
        install_dir = state["install_dir"]

        try:
            # Stop the stack (always)
            self.call_from_thread(
                log.write,
                Text("[•] Stopping BitNest stack...", style="#fdcb6e"),
            )
            subprocess.run(
                ["sudo", "docker", "compose", "-f", compose_file, "down"],
                check=True,
            )
            self.call_from_thread(
                log.write,
                Text("[✔] Stack stopped", style="#00b894"),
            )

            if delete_data:
                # Delete install directory
                self.call_from_thread(
                    log.write,
                    Text(f"[•] Deleting install directory: {install_dir}...", style="#fdcb6e"),
                )
                shutil.rmtree(install_dir, ignore_errors=True)
                self.call_from_thread(
                    log.write,
                    Text("[✔] Install directory deleted", style="#00b894"),
                )

                # Delete state file (last)
                state_path().unlink(missing_ok=True)
                self.call_from_thread(
                    log.write,
                    Text("[✔] State file deleted", style="#00b894"),
                )

                self.call_from_thread(
                    log.write,
                    Text("[✔] BitNest has been uninstalled.", style="#00b894"),
                )
            else:
                # Keep data — just preserve install dir and state file
                self.call_from_thread(
                    log.write,
                    Text(
                        f"[✔] BitNest stopped. Your data is preserved at {install_dir}.",
                        style="#00b894",
                    ),
                )

            # Show Quit button
            self.app.call_from_thread(self._show_quit_button)

        except (subprocess.CalledProcessError, OSError) as exc:
            self.call_from_thread(
                log.write,
                Text(
                    f"[✗] Uninstall failed: {exc}. Press Q to quit.",
                    style="#e17055",
                ),
            )

    def _show_quit_button(self) -> None:
        try:
            self.mount(Button("Quit", id="quit_btn", classes="dim"))
        except Exception:
            pass

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Step 1 — Prerequisites Screen
# ---------------------------------------------------------------------------

class Step1PrerequisitesScreen(Screen):  # type: ignore[misc]
    """Check Docker, ports, and disk space before configuration."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(self) -> None:
        super().__init__()
        self.docker_missing: bool = False
        self.port_conflicts: list[int] = []
        self._checks_done: bool = False

    def compose(self) -> ComposeResult:
        yield StepIndicator(current_step=1)
        with Vertical(id="checks_container"):
            yield Label("[#636e72]  Checking...  [/]", id="docker_status")
            yield Label("[#636e72]  Checking...  [/]", id="compose_status")
            yield Label("[#636e72]  Checking...  [/]", id="port5000_status")
            yield Label("[#636e72]  Checking...  [/]", id="port3000_status")
            yield Label("[#636e72]  Checking...  [/]", id="disk_status")
        yield Horizontal(
            Button("Back", id="back_btn", classes="dim"),
            Button("Next Step", id="next_btn", classes="accent", disabled=True),
        )

    def on_mount(self) -> None:
        self._run_checks()

    @work(exclusive=True, thread=True)
    def _run_checks(self) -> None:
        """Run all prerequisite checks in a background worker."""
        docker_installed, compose_v2 = check_docker()

        if docker_installed:
            self.docker_missing = False
            self.call_from_thread(
                self.query_one("#docker_status", Label).update,
                f"[#00b894]{'✔':3s}[/][white]{'Docker Engine':22s}[/]installed",
            )
        else:
            self.docker_missing = True
            self.call_from_thread(
                self.query_one("#docker_status", Label).update,
                f"[#fdcb6e]{'⏳':3s}[/][white]{'Docker Engine':22s}[/]Docker will be installed automatically",
            )

        if docker_installed:
            version_str = check_compose_version()
            if compose_v2 and version_str:
                short_ver = version_str.split()[-1] if version_str else "v2.x.x"
                self.call_from_thread(
                    self.query_one("#compose_status", Label).update,
                    f"[#00b894]{'✔':3s}[/][white]{'Docker Compose V2':22s}[/]{short_ver}",
                )
            else:
                self.call_from_thread(
                    self.query_one("#compose_status", Label).update,
                    f"[#e17055]{'✗':3s}[/][white]{'Docker Compose V2':22s}[/]not found",
                )
        else:
            self.call_from_thread(
                self.query_one("#compose_status", Label).update,
                f"[#fdcb6e]{'⏳':3s}[/][white]{'Docker Compose V2':22s}[/]Docker will be installed automatically",
            )

        port5000_free = is_port_free(5000)
        if port5000_free:
            self.call_from_thread(
                self.query_one("#port5000_status", Label).update,
                f"[#00b894]{'✔':3s}[/][white]{'Port 5000':22s}[/]free",
            )
        else:
            self.port_conflicts.append(5000)
            self.call_from_thread(
                self.query_one("#port5000_status", Label).update,
                f"[#e17055]{'✗':3s}[/][white]{'Port 5000':22s}[/]in use — change in Step 2",
            )

        port3000_free = is_port_free(3000)
        if port3000_free:
            self.call_from_thread(
                self.query_one("#port3000_status", Label).update,
                f"[#00b894]{'✔':3s}[/][white]{'Port 3000':22s}[/]free",
            )
        else:
            self.port_conflicts.append(3000)
            self.call_from_thread(
                self.query_one("#port3000_status", Label).update,
                f"[#e17055]{'✗':3s}[/][white]{'Port 3000':22s}[/]in use — change in Step 2",
            )

        has_space, free_gb = check_disk_space()
        if has_space:
            self.call_from_thread(
                self.query_one("#disk_status", Label).update,
                f"[#00b894]{'✔':3s}[/][white]{'Disk space':22s}[/]{free_gb:.0f} GB free",
            )
        else:
            self.call_from_thread(
                self.query_one("#disk_status", Label).update,
                f"[#fdcb6e]{'⏳':3s}[/][white]{'Disk space':22s}[/]{free_gb:.0f} GB free — 5 GB recommended",
            )

        self._checks_done = True
        self.call_from_thread(self._enable_next)

    def _enable_next(self) -> None:
        self.query_one("#next_btn", Button).disabled = False

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "back_btn":
            self.app.pop_screen()
        elif event.button.id == "next_btn" and self._checks_done:
            default_api = 5000 if 5000 not in self.port_conflicts else 5001
            default_frontend = 3000 if 3000 not in self.port_conflicts else 3001
            self.app.push_screen(
                Step2ConfigurationScreen(
                    docker_missing=self.docker_missing,
                    default_api_port=default_api,
                    default_frontend_port=default_frontend,
                )
            )

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Step 2 — Configuration Screen
# ---------------------------------------------------------------------------

class Step2ConfigurationScreen(Screen):  # type: ignore[misc]
    """Collect install directory, ports, and admin credentials."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(
        self,
        docker_missing: bool = False,
        default_api_port: int = 5000,
        default_frontend_port: int = 3000,
    ) -> None:
        super().__init__()
        self.docker_missing = docker_missing
        self.default_api_port = default_api_port
        self.default_frontend_port = default_frontend_port

    def compose(self) -> ComposeResult:
        yield StepIndicator(current_step=2)
        with Vertical():
            yield Label("Install directory")
            yield Input(value="~/bitnest", id="install_dir")
            yield Label("", id="install_dir_error", classes="destructive dim")

            yield Label("API port")
            yield Input(value=str(self.default_api_port), id="api_port")
            yield Label("", id="api_port_error", classes="destructive dim")

            yield Label("Frontend port")
            yield Input(value=str(self.default_frontend_port), id="frontend_port")
            yield Label("", id="frontend_port_error", classes="destructive dim")

            yield Label("Admin username  (min 3 characters)")
            yield Input(id="admin_user")
            yield Label("", id="admin_user_error", classes="destructive dim")

            yield Label("Admin password  (min 8 characters, hidden)")
            yield Input(password=True, id="admin_pass")
            yield Label("", id="admin_pass_error", classes="destructive dim")

        yield Horizontal(
            Button("Back", id="back_btn", classes="dim"),
            Button("Next Step", id="next_btn", classes="accent"),
        )

    def _validate_and_advance(self) -> None:
        """Validate all fields and push Step3 if all valid."""
        install_dir_val = self.query_one("#install_dir", Input).value
        api_port_val = self.query_one("#api_port", Input).value
        frontend_port_val = self.query_one("#frontend_port", Input).value
        admin_user_val = self.query_one("#admin_user", Input).value
        admin_pass_val = self.query_one("#admin_pass", Input).value

        has_errors = False

        # Validate install dir
        ok, msg = validate_path(install_dir_val)
        error_label = self.query_one("#install_dir_error", Label)
        if ok:
            resolved_dir = msg
            error_label.update("")
        else:
            error_label.update(msg)
            has_errors = True
            resolved_dir = ""

        # Validate API port
        ok, msg = validate_port(api_port_val)
        error_label = self.query_one("#api_port_error", Label)
        if ok:
            error_label.update("")
        else:
            error_label.update(msg)
            has_errors = True

        # Validate frontend port
        ok, msg = validate_port(frontend_port_val)
        error_label = self.query_one("#frontend_port_error", Label)
        if ok:
            error_label.update("")
        else:
            error_label.update(msg)
            has_errors = True

        # Re-check port conflicts if ports are valid integers
        if not has_errors or (
            validate_port(api_port_val)[0] and validate_port(frontend_port_val)[0]
        ):
            for port_id, port_val_str in [
                ("api_port_error", api_port_val),
                ("frontend_port_error", frontend_port_val),
            ]:
                v_ok, _ = validate_port(port_val_str)
                if v_ok:
                    port_num = int(port_val_str)
                    if not is_port_free(port_num):
                        self.query_one(f"#{port_id}", Label).update(
                            f"Port {port_num} is in use. Choose a different port."
                        )
                        has_errors = True

        # Validate admin username
        ok, msg = validate_username(admin_user_val)
        error_label = self.query_one("#admin_user_error", Label)
        if ok:
            error_label.update("")
        else:
            error_label.update(msg)
            has_errors = True

        # Validate admin password
        ok, msg = validate_password(admin_pass_val)
        error_label = self.query_one("#admin_pass_error", Label)
        if ok:
            error_label.update("")
        else:
            error_label.update(msg)
            has_errors = True

        if has_errors:
            return

        # All valid — collect config and advance
        config = {
            "install_dir": resolved_dir,
            "api_port": int(api_port_val),
            "frontend_port": int(frontend_port_val),
            "admin_user": admin_user_val,
            "admin_pass": admin_pass_val,
            "db_password": generate_secret(),
            "jwt_key": generate_secret(),
            "docker_missing": self.docker_missing,
        }
        self.app.push_screen(Step3InstallingScreen(config))

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "back_btn":
            self.app.pop_screen()
        elif event.button.id == "next_btn":
            self._validate_and_advance()

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Step 3 — Installing Screen
# ---------------------------------------------------------------------------

class Step3InstallingScreen(Screen):  # type: ignore[misc]
    """Run the full installation sequence with live log output."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(self, config: dict) -> None:
        super().__init__()
        self.config = config

    def compose(self) -> ComposeResult:
        yield StepIndicator(current_step=3)
        yield RichLog(id="install_log", highlight=True, markup=True)

    def on_mount(self) -> None:
        self.run_install()

    @work(exclusive=True, thread=True)
    def run_install(self) -> None:
        """Execute the full installation sequence in a background worker."""
        log = self.query_one("#install_log", RichLog)

        try:
            # Step 3.1 — Install Docker if missing
            if self.config["docker_missing"]:
                self.call_from_thread(
                    log.write,
                    Text.from_ansi("[•] Installing Docker Engine..."),
                )
                info = read_os_release()
                path = get_docker_install_path(info)
                commands = get_docker_install_commands(path)
                for cmd in commands:
                    self.call_from_thread(
                        log.write,
                        Text(f"    → Running: {' '.join(cmd)}", style="dim"),
                    )
                    result = subprocess.run(cmd, capture_output=True, text=True)
                    if result.returncode != 0:
                        self.call_from_thread(
                            log.write,
                            Text(
                                f"[✗] Docker installation failed: {result.stderr.strip()}",
                                style="#e17055",
                            ),
                        )
                        self.call_from_thread(
                            log.write,
                            Text(
                                "Installation failed: Docker install error. Press Q to quit.",
                                style="#e17055",
                            ),
                        )
                        return
                self.call_from_thread(
                    log.write,
                    Text("[✔] Docker Engine installed", style="#00b894"),
                )

            # Step 3.2 — Add user to docker group
            username = os.environ.get("USER", os.environ.get("LOGNAME", ""))
            subprocess.run(
                ["sudo", "usermod", "-aG", "docker", username],
                check=False,
            )
            self.call_from_thread(
                log.write,
                Text("[✔] User added to docker group", style="#00b894"),
            )

            # Step 3.3 — Create install directory
            install_dir = Path(self.config["install_dir"])
            create_install_dirs(install_dir)
            self.call_from_thread(
                log.write,
                Text(f"[✔] Install directory created: {install_dir}", style="#00b894"),
            )

            # Step 3.4 — Write compose.yaml
            compose_content = render_compose(
                api_port=self.config["api_port"],
                frontend_port=self.config["frontend_port"],
            )
            compose_path = install_dir / "compose.yaml"
            compose_path.write_text(compose_content)
            self.call_from_thread(
                log.write,
                Text("[✔] compose.yaml written", style="#00b894"),
            )

            # Step 3.5 — Write .env
            env_path = install_dir / ".env"
            write_env_file(env_path, self.config)
            self.call_from_thread(
                log.write,
                Text("[✔] .env written (chmod 600)", style="#00b894"),
            )

            # Step 3.6 — Pull images with streaming output
            self.call_from_thread(
                log.write,
                Text("[•] Pulling images...", style="#fdcb6e"),
            )
            cmd = [
                "sudo", "docker", "compose", "-f", str(compose_path), "pull"
            ]
            with subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
            ) as proc:
                for line in proc.stdout:  # type: ignore[union-attr]
                    stripped = line.rstrip()
                    if stripped:
                        self.call_from_thread(
                            log.write,
                            Text(f"    → {stripped}", style="dim"),
                        )
            if proc.returncode != 0:
                self.call_from_thread(
                    log.write,
                    Text("[✗] Image pull failed. Press Q to quit.", style="#e17055"),
                )
                return
            self.call_from_thread(
                log.write,
                Text("[✔] Images pulled", style="#00b894"),
            )

            # Step 3.7 — Start stack
            subprocess.run(
                [
                    "sudo", "docker", "compose", "-f", str(compose_path),
                    "up", "-d",
                ],
                check=True,
            )
            self.call_from_thread(
                log.write,
                Text("[✔] Stack started", style="#00b894"),
            )

            # Step 3.8 — Health poll
            self.call_from_thread(
                log.write,
                Text(
                    "[•] Waiting for services to become healthy...",
                    style="#fdcb6e",
                ),
            )
            health = poll_health(str(compose_path), timeout=60, use_sudo=True)
            for svc, ok in sorted(health.items()):
                icon = "[✔]" if ok else "[✗]"
                color = "#00b894" if ok else "#e17055"
                self.call_from_thread(
                    log.write,
                    Text(
                        f"    {svc:12s} {icon} {'healthy' if ok else 'unhealthy'}",
                        style=color,
                    ),
                )

            if all(health.values()):
                write_state(
                    install_dir=str(install_dir),
                    api_port=self.config["api_port"],
                    frontend_port=self.config["frontend_port"],
                    compose_file=str(compose_path),
                )
                self.app.call_from_thread(
                    self.app.push_screen,
                    Step4DoneScreen(self.config),
                )
            else:
                self.call_from_thread(
                    log.write,
                    Text(
                        "[✗] Some services failed to start. Press Q to quit.",
                        style="#e17055",
                    ),
                )

        except (subprocess.CalledProcessError, OSError) as exc:
            self.call_from_thread(
                log.write,
                Text(
                    f"[✗] Installation failed: {exc}. Press Q to quit.",
                    style="#e17055",
                ),
            )

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# Step 4 — Done Screen (Success)
# ---------------------------------------------------------------------------

class Step4DoneScreen(Screen):  # type: ignore[misc]
    """Display installation success with service URLs and admin details."""

    BINDINGS = [Binding("q", "quit", "Quit")]

    def __init__(self, config: dict) -> None:
        super().__init__()
        self.config = config

    def compose(self) -> ComposeResult:
        yield StepIndicator(current_step=4)
        yield Label("[bold #00b894]✔  All services healthy[/]")
        yield Label(f"   Frontend:  http://localhost:{self.config['frontend_port']}")
        yield Label(f"   API:       http://localhost:{self.config['api_port']}")
        yield Label("")
        yield Label("   Your admin account has been created.")
        yield Label(f"   Username:  {self.config['admin_user']}")
        yield Label("   Open your browser to get started.")
        yield Label("")
        yield Label(
            "   Run this installer again to update or uninstall BitNest.",
            classes="dim",
        )
        yield Button("Quit", id="quit_btn", classes="dim")

    def on_button_pressed(self, event: Button.Pressed) -> None:
        if event.button.id == "quit_btn":
            self.app.exit()

    def action_quit(self) -> None:
        self.app.exit()


# ---------------------------------------------------------------------------
# InstallerApp
# ---------------------------------------------------------------------------

class InstallerApp(App):
    """BitNest TUI Installer application."""

    CSS = """
Screen {
    background: #1a1a2e;
    color: #dfe6e9;
    padding: 8;
}

#title {
    text-style: bold;
    color: #e94560;
    margin-bottom: 2;
}

.step-indicator {
    background: #16213e;
    padding: 1;
    margin-bottom: 2;
}

.step-active {
    color: #e94560;
    text-style: bold;
}

.step-inactive {
    color: #636e72;
}

.success {
    color: #00b894;
}

.warning {
    color: #fdcb6e;
}

.destructive {
    color: #e17055;
}

.dim {
    color: #636e72;
}

Button {
    margin: 1 2;
}

Button.accent {
    background: #e94560;
}

Button.dim {
    background: #16213e;
    color: #636e72;
}

Input:focus {
    border: tall #e94560;
}

ListView > ListItem.-highlight {
    background: #e94560;
}
"""

    TITLE = "BitNest Installer"
    BINDINGS = [Binding("q", "quit", "Quit")]

    def on_mount(self) -> None:
        self.push_screen(MainMenuScreen())


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    app = InstallerApp()
    app.run()

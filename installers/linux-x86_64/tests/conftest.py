"""Shared fixtures for installer test suite."""
import subprocess
import pytest


@pytest.fixture
def mock_os_release(monkeypatch):
    """Patches Path('/etc/os-release').read_text to return custom content."""
    def _factory(content: str):
        import pathlib
        original_read_text = pathlib.Path.read_text

        def patched_read_text(self, *args, **kwargs):
            if str(self) == "/etc/os-release":
                return content
            return original_read_text(self, *args, **kwargs)

        monkeypatch.setattr(pathlib.Path, "read_text", patched_read_text)

    return _factory


@pytest.fixture
def mock_subprocess(monkeypatch):
    """Patches subprocess.run to return a configurable CompletedProcess."""
    def _factory(returncode: int = 0, stdout: str = "", stderr: str = ""):
        def fake_run(cmd, *args, **kwargs):
            return subprocess.CompletedProcess(
                args=cmd,
                returncode=returncode,
                stdout=stdout,
                stderr=stderr,
            )

        monkeypatch.setattr(subprocess, "run", fake_run)

    return _factory

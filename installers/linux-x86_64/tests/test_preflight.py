"""Tests for preflight check functions in install.py."""
import sys
import os
import socket
import subprocess
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestIsPortFree:
    def test_occupied_port_returns_false(self):
        # Bind a port ourselves, then check that is_port_free returns False
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            s.bind(("0.0.0.0", 0))
            port = s.getsockname()[1]
            result = install.is_port_free(port)
        assert result is False

    def test_free_port_returns_true(self):
        # Use a high port that is unlikely to be in use
        # Find a free port first
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.bind(("0.0.0.0", 0))
            port = s.getsockname()[1]
        # After closing, port should be free
        result = install.is_port_free(port)
        assert result is True


class TestCheckDiskSpace:
    def test_returns_bool_float_tuple(self):
        result = install.check_disk_space("/")
        assert isinstance(result, tuple)
        assert len(result) == 2
        ok, free_gb = result
        assert isinstance(ok, bool)
        assert isinstance(free_gb, float)

    def test_free_gb_is_positive(self):
        _, free_gb = install.check_disk_space("/")
        assert free_gb > 0


class TestCheckDocker:
    def test_returns_two_bools(self, monkeypatch):
        # Mock shutil.which and subprocess.run
        import shutil
        monkeypatch.setattr(shutil, "which", lambda x: "/usr/bin/docker")
        monkeypatch.setattr(subprocess, "run", lambda *a, **kw: subprocess.CompletedProcess(
            args=a[0], returncode=0, stdout="Docker Compose version v2.24.0", stderr=""
        ))
        docker_installed, compose_v2 = install.check_docker()
        assert docker_installed is True
        assert compose_v2 is True

    def test_docker_not_installed(self, monkeypatch):
        import shutil
        monkeypatch.setattr(shutil, "which", lambda x: None)
        docker_installed, compose_v2 = install.check_docker()
        assert docker_installed is False
        assert compose_v2 is False

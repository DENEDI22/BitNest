"""Tests for state file read/write in install.py."""
import sys
import os
import json
import pathlib
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestStatePath:
    def test_ends_with_bitnest_install_json(self):
        p = install.state_path()
        assert str(p).endswith(".config/bitnest/install.json")

    def test_uses_xdg_config_home(self, monkeypatch, tmp_path):
        xdg_dir = tmp_path / "xdg_config"
        monkeypatch.setenv("XDG_CONFIG_HOME", str(xdg_dir))
        p = install.state_path()
        assert str(p).startswith(str(xdg_dir))
        assert p.name == "install.json"


class TestWriteReadStateRoundtrip:
    def test_roundtrip(self, monkeypatch, tmp_path):
        state_file = tmp_path / "bitnest" / "install.json"
        monkeypatch.setenv("XDG_CONFIG_HOME", str(tmp_path))

        install.write_state(
            install_dir="/home/user/bitnest",
            api_port=5000,
            frontend_port=3000,
            compose_file="/home/user/bitnest/compose.yaml",
        )

        result = install.read_state()
        assert result is not None
        assert result["install_dir"] == "/home/user/bitnest"
        assert result["api_port"] == 5000
        assert result["frontend_port"] == 3000
        assert result["compose_file"] == "/home/user/bitnest/compose.yaml"
        assert "installed_at" in result

    def test_read_state_returns_none_when_missing(self, monkeypatch, tmp_path):
        # Point XDG_CONFIG_HOME to an empty temp dir
        monkeypatch.setenv("XDG_CONFIG_HOME", str(tmp_path / "nonexistent"))
        result = install.read_state()
        assert result is None

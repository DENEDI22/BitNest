"""Tests for filesystem operations in install.py."""
import sys
import os
import stat
import pathlib
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestCreateInstallDirs:
    def test_creates_storage_and_postgres_subdirs(self, tmp_path):
        install_dir = tmp_path / "bitnest"
        install.create_install_dirs(install_dir)
        assert (install_dir / "data" / "storage").is_dir()
        assert (install_dir / "data" / "postgres").is_dir()

    def test_idempotent(self, tmp_path):
        install_dir = tmp_path / "bitnest"
        install.create_install_dirs(install_dir)
        install.create_install_dirs(install_dir)  # should not raise
        assert (install_dir / "data" / "storage").is_dir()


class TestWriteEnvFile:
    def test_creates_file_with_chmod_600(self, tmp_path):
        env_path = tmp_path / ".env"
        cfg = {
            "install_dir": str(tmp_path),
            "db_password": "abc123",
            "jwt_key": "deadbeef",
            "admin_user": "admin",
            "admin_pass": "supersecret",
        }
        install.write_env_file(env_path, cfg)
        assert env_path.exists()
        mode = oct(stat.S_IMODE(os.stat(env_path).st_mode))
        assert mode == oct(0o600), f"Expected 600, got {mode}"

    def test_contains_all_required_vars(self, tmp_path):
        env_path = tmp_path / ".env"
        cfg = {
            "install_dir": str(tmp_path),
            "db_password": "dbpass",
            "jwt_key": "jwtkey",
            "admin_user": "adminuser",
            "admin_pass": "adminpass",
        }
        install.write_env_file(env_path, cfg)
        content = env_path.read_text()
        assert "DATA_DIR=" in content
        assert "POSTGRES_PASSWORD=" in content
        assert "AUTH_SIGNING_KEY=" in content
        assert "BITNEST_ADMIN_USER=" in content
        assert "BITNEST_ADMIN_PASS=" in content

    def test_values_are_written(self, tmp_path):
        env_path = tmp_path / ".env"
        cfg = {
            "install_dir": "/home/user/bitnest",
            "db_password": "dbpass123",
            "jwt_key": "jwtkey123",
            "admin_user": "myadmin",
            "admin_pass": "mypassword",
        }
        install.write_env_file(env_path, cfg)
        content = env_path.read_text()
        assert "DATA_DIR=/home/user/bitnest" in content
        assert "POSTGRES_PASSWORD=dbpass123" in content
        assert "AUTH_SIGNING_KEY=jwtkey123" in content
        assert "BITNEST_ADMIN_USER=myadmin" in content
        assert "BITNEST_ADMIN_PASS=mypassword" in content

"""Tests for validation functions in install.py."""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestValidatePort:
    def test_valid_port(self):
        ok, msg = install.validate_port("5000")
        assert ok is True
        assert msg == ""

    def test_valid_lower_boundary(self):
        ok, _ = install.validate_port("1024")
        assert ok is True

    def test_valid_upper_boundary(self):
        ok, _ = install.validate_port("65535")
        assert ok is True

    def test_port_zero(self):
        ok, msg = install.validate_port("0")
        assert ok is False
        assert "1024" in msg and "65535" in msg

    def test_port_above_max(self):
        ok, msg = install.validate_port("65536")
        assert ok is False

    def test_port_below_min(self):
        ok, msg = install.validate_port("1023")
        assert ok is False

    def test_non_numeric(self):
        ok, _ = install.validate_port("abc")
        assert ok is False

    def test_empty(self):
        ok, _ = install.validate_port("")
        assert ok is False


class TestValidatePath:
    def test_valid_home_relative(self):
        ok, resolved = install.validate_path("~/bitnest")
        assert ok is True
        assert resolved.startswith("/")
        assert "~" not in resolved

    def test_empty_path(self):
        ok, msg = install.validate_path("")
        assert ok is False
        assert "valid path" in msg.lower() or "bitnest" in msg.lower()

    def test_absolute_path(self):
        ok, resolved = install.validate_path("/tmp/bitnest")
        assert ok is True
        assert resolved == "/tmp/bitnest"


class TestValidateUsername:
    def test_valid_username(self):
        ok, msg = install.validate_username("abc")
        assert ok is True
        assert msg == ""

    def test_too_short(self):
        ok, msg = install.validate_username("ab")
        assert ok is False
        assert "3" in msg

    def test_empty(self):
        ok, _ = install.validate_username("")
        assert ok is False

    def test_long_username(self):
        ok, _ = install.validate_username("verylongusername")
        assert ok is True


class TestValidatePassword:
    def test_valid_password(self):
        ok, msg = install.validate_password("12345678")
        assert ok is True
        assert msg == ""

    def test_too_short(self):
        ok, msg = install.validate_password("1234567")
        assert ok is False
        assert "8" in msg

    def test_empty(self):
        ok, _ = install.validate_password("")
        assert ok is False

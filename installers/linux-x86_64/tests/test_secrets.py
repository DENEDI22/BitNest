"""Tests for secret generation in install.py."""
import sys
import os
import re
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestGenerateSecret:
    def test_returns_64_char_string(self):
        secret = install.generate_secret()
        assert len(secret) == 64

    def test_hex_only(self):
        secret = install.generate_secret()
        assert re.fullmatch(r"[0-9a-f]{64}", secret), f"Not hex-only: {secret}"

    def test_unique_values(self):
        s1 = install.generate_secret()
        s2 = install.generate_secret()
        assert s1 != s2

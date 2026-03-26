"""Tests for compose template rendering in install.py."""
import sys
import os
import yaml
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestRenderCompose:
    def setup_method(self):
        self.rendered = install.render_compose(
            api_port=5000,
            frontend_port=3000,
            docker_hub_user="denedi22",
        )

    def test_produces_valid_yaml(self):
        parsed = yaml.safe_load(self.rendered)
        assert parsed is not None
        assert "services" in parsed

    def test_contains_pg_isready(self):
        assert "pg_isready -U bitnest" in self.rendered

    def test_contains_service_healthy(self):
        assert "condition: service_healthy" in self.rendered

    def test_contains_postgres_password_docker_var(self):
        assert "${POSTGRES_PASSWORD}" in self.rendered

    def test_contains_docker_hub_image(self):
        assert "denedi22/bitnest_api:latest" in self.rendered

    def test_no_tilde_in_output(self):
        assert "~/" not in self.rendered

    def test_no_version_key(self):
        # Docker Compose v2 does not need a top-level version: key
        assert "version:" not in self.rendered

    def test_restart_unless_stopped_api(self):
        assert "restart: unless-stopped" in self.rendered

    def test_contains_admin_user_env(self):
        assert "BITNEST_ADMIN_USER" in self.rendered

    def test_contains_admin_pass_env(self):
        assert "BITNEST_ADMIN_PASS" in self.rendered

    def test_api_port_appears(self):
        assert "5000:8080" in self.rendered

    def test_frontend_port_appears(self):
        assert "3000:80" in self.rendered

    def test_frontend_image(self):
        assert "denedi22/bitnest_frontend:latest" in self.rendered

    def test_default_docker_hub_user(self):
        rendered_default = install.render_compose(api_port=5000, frontend_port=3000)
        assert "denedi22/bitnest_api:latest" in rendered_default

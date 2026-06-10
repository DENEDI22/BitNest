"""Tests for health polling functions in install.py."""
import sys
import os
import json
import subprocess
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


class TestParseComposePsJson:
    def test_healthy_service(self):
        data = [{"Name": "bitnest_api", "State": "running", "Health": "healthy"}]
        stdout = "\n".join(json.dumps(item) for item in data)
        result = install.parse_compose_ps_json(stdout)
        assert result["bitnest_api"] is True

    def test_running_without_healthcheck(self):
        data = [{"Name": "bitnest_frontend", "State": "running", "Health": ""}]
        stdout = "\n".join(json.dumps(item) for item in data)
        result = install.parse_compose_ps_json(stdout)
        assert result["bitnest_frontend"] is True

    def test_exited_service_is_false(self):
        data = [{"Name": "bitnest_db", "State": "exited", "Health": ""}]
        stdout = "\n".join(json.dumps(item) for item in data)
        result = install.parse_compose_ps_json(stdout)
        assert result["bitnest_db"] is False

    def test_unhealthy_service(self):
        data = [{"Name": "bitnest_api", "State": "running", "Health": "unhealthy"}]
        stdout = "\n".join(json.dumps(item) for item in data)
        result = install.parse_compose_ps_json(stdout)
        assert result["bitnest_api"] is False

    def test_multiple_services(self):
        data = [
            {"Name": "bitnest_api", "State": "running", "Health": "healthy"},
            {"Name": "bitnest_db", "State": "running", "Health": "healthy"},
            {"Name": "bitnest_frontend", "State": "running", "Health": ""},
        ]
        stdout = "\n".join(json.dumps(item) for item in data)
        result = install.parse_compose_ps_json(stdout)
        assert result["bitnest_api"] is True
        assert result["bitnest_db"] is True
        assert result["bitnest_frontend"] is True

    def test_empty_output(self):
        result = install.parse_compose_ps_json("")
        assert result == {}

    def test_starting_health_is_false(self):
        data = [{"Name": "bitnest_db", "State": "running", "Health": "starting"}]
        stdout = "\n".join(json.dumps(item) for item in data)
        result = install.parse_compose_ps_json(stdout)
        assert result["bitnest_db"] is False

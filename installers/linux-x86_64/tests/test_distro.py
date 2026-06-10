"""Tests for distro detection functions in install.py."""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import install


def make_os_release(id_val, id_like=""):
    lines = [f'ID={id_val}']
    if id_like:
        lines.append(f'ID_LIKE={id_like}')
    return "\n".join(lines)


class TestGetDockerInstallPath:
    def test_ubuntu(self):
        info = {"ID": "ubuntu"}
        assert install.get_docker_install_path(info) == "apt_ubuntu"

    def test_debian(self):
        info = {"ID": "debian"}
        assert install.get_docker_install_path(info) == "apt_debian"

    def test_fedora(self):
        info = {"ID": "fedora"}
        assert install.get_docker_install_path(info) == "dnf_fedora"

    def test_arch(self):
        info = {"ID": "arch"}
        assert install.get_docker_install_path(info) == "pacman"

    def test_rhel(self):
        info = {"ID": "rhel"}
        assert install.get_docker_install_path(info) == "dnf_rhel"

    def test_unknown(self):
        info = {"ID": "slackware"}
        assert install.get_docker_install_path(info) == "fallback"

    def test_id_like_debian(self):
        info = {"ID": "linuxmint", "ID_LIKE": "ubuntu debian"}
        result = install.get_docker_install_path(info)
        # Should match ubuntu or debian via ID_LIKE
        assert result in ("apt_ubuntu", "apt_debian")

    def test_raspbian_maps_to_apt_debian(self):
        info = {"ID": "raspbian"}
        assert install.get_docker_install_path(info) == "apt_debian"


class TestReadOsRelease:
    def test_parses_key_value_pairs(self, monkeypatch):
        content = 'ID=ubuntu\nID_LIKE="debian"\nVERSION_ID="22.04"\n'
        import pathlib

        original = pathlib.Path.read_text

        def patched(self, *args, **kwargs):
            if str(self) == "/etc/os-release":
                return content
            return original(self, *args, **kwargs)

        monkeypatch.setattr(pathlib.Path, "read_text", patched)
        result = install.read_os_release()
        assert result["ID"] == "ubuntu"
        assert result["ID_LIKE"] == "debian"
        assert result["VERSION_ID"] == "22.04"

    def test_returns_empty_on_missing_file(self, monkeypatch):
        import pathlib

        def patched(self, *args, **kwargs):
            if str(self) == "/etc/os-release":
                raise OSError("not found")
            return pathlib.Path.read_text(self, *args, **kwargs)

        monkeypatch.setattr(pathlib.Path, "read_text", patched)
        result = install.read_os_release()
        assert result == {}

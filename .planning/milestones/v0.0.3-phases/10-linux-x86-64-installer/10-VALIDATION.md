---
phase: 10
slug: linux-x86-64-installer
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-26
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | pytest 7.x |
| **Config file** | installer/tests/conftest.py — Wave 0 installs |
| **Quick run command** | `python -m pytest installer/tests/unit/ -q` |
| **Full suite command** | `python -m pytest installer/tests/ -q` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `python -m pytest installer/tests/unit/ -q`
- **After every plan wave:** Run `python -m pytest installer/tests/ -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-01-01 | 01 | 0 | INST-01 | unit | `python -m pytest installer/tests/unit/test_validation.py -q` | ❌ W0 | ⬜ pending |
| 10-01-02 | 01 | 0 | INST-02 | unit | `python -m pytest installer/tests/unit/test_distro.py -q` | ❌ W0 | ⬜ pending |
| 10-01-03 | 01 | 1 | INST-03 | unit | `python -m pytest installer/tests/unit/test_secrets.py -q` | ❌ W0 | ⬜ pending |
| 10-01-04 | 01 | 1 | INST-04 | unit | `python -m pytest installer/tests/unit/test_compose_template.py -q` | ❌ W0 | ⬜ pending |
| 10-01-05 | 01 | 1 | INST-05 | unit | `python -m pytest installer/tests/unit/test_state.py -q` | ❌ W0 | ⬜ pending |
| 10-02-01 | 02 | 2 | INST-06 | manual | N/A — TUI screens require interactive terminal | N/A | ⬜ pending |
| 10-02-02 | 02 | 2 | INST-07 | manual | N/A — TUI Back/Next navigation requires interactive terminal | N/A | ⬜ pending |
| 10-02-03 | 02 | 2 | INST-08 | manual | N/A — TUI inline validation display requires interactive terminal | N/A | ⬜ pending |
| 10-03-01 | 03 | 2 | INST-09 | manual | N/A — Live docker pull streaming requires running Docker daemon | N/A | ⬜ pending |
| 10-03-02 | 03 | 2 | INST-10 | manual | N/A — Service health check display requires running containers | N/A | ⬜ pending |
| 10-03-03 | 03 | 3 | INST-11 | manual | N/A — Update flow requires running stack | N/A | ⬜ pending |
| 10-03-04 | 03 | 3 | INST-12 | manual | N/A — Uninstall flow requires running stack and data | N/A | ⬜ pending |
| 10-04-01 | 04 | 1 | LINUX-01 | unit | `python -m pytest installer/tests/unit/test_distro.py -q` | ❌ W0 | ⬜ pending |
| 10-04-02 | 04 | 1 | LINUX-02 | unit | `python -m pytest installer/tests/unit/test_docker_install.py -q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `installer/tests/__init__.py` — package marker
- [ ] `installer/tests/unit/__init__.py` — package marker
- [ ] `installer/tests/conftest.py` — shared fixtures (mock subprocess, mock os.getuid)
- [ ] `installer/tests/unit/test_validation.py` — stubs for INST-01 (port/path validation logic)
- [ ] `installer/tests/unit/test_distro.py` — stubs for INST-02, LINUX-01 (distro detection)
- [ ] `installer/tests/unit/test_secrets.py` — stubs for INST-03 (password/JWT generation)
- [ ] `installer/tests/unit/test_compose_template.py` — stubs for INST-04 (compose.yaml rendering)
- [ ] `installer/tests/unit/test_state.py` — stubs for INST-05 (install_state.json read/write)
- [ ] `installer/tests/unit/test_docker_install.py` — stubs for LINUX-02 (docker install command selection)

*pytest must be available in the test environment: `pip install pytest`*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| TUI wizard displays correctly with Back/Next navigation | INST-06, INST-07 | Requires interactive TTY; Textual screens cannot be driven headlessly in CI | Run `python installer/install.py` on a real terminal, step through all 4 wizard screens |
| Inline validation feedback shows in TUI | INST-08 | UI widget validation messages are not captured by stdout/stderr | Enter invalid port (e.g. 99999) and verify error message appears below the Input widget |
| Live docker pull output streams to RichLog | INST-09 | Requires running Docker daemon and network access | Run fresh install, verify each image layer appears line-by-line during pull |
| Per-service health pass/fail display | INST-10 | Requires running Docker containers responding to healthchecks | After pull completes, verify table shows ✅/❌ per service (api, frontend, postgres, redis) |
| Update flow: rolling restart | INST-11 | Requires a previously installed running stack | Re-run installer on a stack, choose "Update", verify `docker compose pull` + restart sequence |
| Uninstall flow: confirmation required | INST-12 | Requires data on disk and running containers | Re-run installer, choose "Uninstall", verify explicit text confirmation prompt before deletion |
| Docker auto-install on distro without Docker | LINUX-02 | Requires a real or VM machine without Docker | On a fresh Debian/Ubuntu/Fedora/RHEL/Arch VM, run installer and verify Docker gets installed |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

---
phase: 10-linux-x86-64-installer
plan: "03"
subsystem: installer-tui
tags: [installer, python, textual, tui, docker-compose, step3, step4, update, uninstall]
dependency_graph:
  requires:
    - installer-core-logic (10-01)
    - installer-tui-shell (10-02)
  provides:
    - installer-step3-installing-screen
    - installer-step4-done-screen
    - installer-update-screen
    - installer-uninstall-screens
    - complete-installer
  affects:
    - installers/linux-x86_64/install.py
tech_stack:
  added: []
  patterns:
    - RichLog with call_from_thread for cross-thread UI updates
    - subprocess.Popen for streaming docker pull output to RichLog
    - "@work(exclusive=True) for all long-running installation operations"
    - Two-phase uninstall confirmation (UninstallScreen -> UninstallConfirm2Screen)
    - No docker compose down in update flow (per D-21)
    - State file written on success, deleted after full uninstall (last)
key_files:
  created: []
  modified:
    - installers/linux-x86_64/install.py
decisions:
  - Text stub class added to import-fallback section so RichLog.write(Text(...)) works without textual installed
  - UpdateScreen compose down appears only in comments explaining it is NOT used; actual code has no down command
  - UninstallConfirm2Screen uses mount() to append Quit button after worker completes
  - Keep My Data still stops the stack (docker compose down) but preserves the install directory and state file
metrics:
  duration: 218s
  completed: "2026-03-26T10:46:07Z"
  tasks_completed: 2
  files_modified: 1
---

# Phase 10 Plan 03: Step3/Step4/Update/Uninstall TUI Screens Summary

**One-liner:** Complete Textual TUI installer with Step3 (Docker install + image pull + health poll), Step4 (success screen), UpdateScreen (pull+restart+health), and two-phase UninstallScreen — all 8 Screen subclasses fully implemented.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Step3InstallingScreen + Step4DoneScreen | ec0379b | installers/linux-x86_64/install.py |
| 2 | UpdateScreen + UninstallScreen + UninstallConfirm2Screen | bdc6cf3 | installers/linux-x86_64/install.py |

## What Was Built

### Task 1: Step3InstallingScreen + Step4DoneScreen

**Step3InstallingScreen** replaces the Plan 02 stub with the full 8-step installation sequence:

1. Docker Engine install (if `docker_missing=True`) — per-distro commands from `get_docker_install_commands`, each command streamed to `RichLog` with dim text
2. Add current user to docker group (`sudo usermod -aG docker $USER`); all remaining compose calls use `sudo docker`
3. Create install directories via `create_install_dirs(install_dir)`
4. Write `compose.yaml` via `render_compose()` to `{install_dir}/compose.yaml`
5. Write `.env` via `write_env_file()` (chmod 600 applied inside function)
6. Pull images: `subprocess.Popen(["sudo", "docker", "compose", "-f", ..., "pull"])` with stdout streaming to `RichLog` line by line
7. Start stack: `subprocess.run([... "up", "-d"], check=True)`
8. Health poll: `poll_health(str(compose_path), timeout=60, use_sudo=True)` with per-service ✔/✗ status written to log

On success: calls `write_state(...)` then `self.app.call_from_thread(self.app.push_screen, Step4DoneScreen(config))` to auto-advance.

On failure: shows error in destructive color `#e17055` and instructs user to press Q.

**Step4DoneScreen** shows:
- `✔  All services healthy` in bold success green
- Frontend and API URLs
- Admin username (password NOT shown per D-12)
- Re-run hint in dim style
- Quit button to exit app

### Task 2: UpdateScreen + UninstallScreen + UninstallConfirm2Screen

**UpdateScreen** (full implementation replacing Plan 02 stub):
- Reads state via `read_state()` on compose; shows installed path and timestamp
- "Update BitNest" button disables Back button, streams pull output via `Popen`, then `up -d`, then health poll
- No `docker compose down` per D-21
- Shows Quit button after completion

**UninstallScreen** (full implementation replacing Plan 02 stub):
- Reads state via `read_state()` on compose
- Shows "This will stop BitNest and remove the installation." + install directory
- "Continue to Uninstall" pushes `UninstallConfirm2Screen(state)`
- Back pops to main menu

**UninstallConfirm2Screen** (new screen):
- Full destructive styling: all warning text in `#e17055`
- Shows "Delete all data?", "This cannot be undone.", directory to be deleted
- "Delete Everything": `compose down` → `shutil.rmtree(install_dir)` → `state_path().unlink()` (state file deleted last per D-22)
- "Keep My Data": `compose down` only — preserves install directory and state file, shows preservation message
- Quit button appears after completion

### Textual import stubs updated
Added `RichLog` and `Text` (from `rich.text`) to both the live import block and the fallback stub block so unit tests (which don't have textual installed) can still import the module cleanly.

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None. All 8 Screen subclasses are fully implemented:
- `MainMenuScreen` — Plan 02
- `Step1PrerequisitesScreen` — Plan 02
- `Step2ConfigurationScreen` — Plan 02
- `Step3InstallingScreen` — This plan
- `Step4DoneScreen` — This plan
- `UpdateScreen` — This plan (replaced Plan 02 stub)
- `UninstallScreen` — This plan (replaced Plan 02 stub)
- `UninstallConfirm2Screen` — This plan (new)

## Verification Results

- `python -c "import ast; ast.parse(...)"` — SYNTAX OK
- `python -m pytest installers/linux-x86_64/tests/ -x -q` — 67 passed (no regressions)
- `grep -c "class.*Screen" install.py` — 9 (8 functional + 1 fallback stub)
- `grep -c "@work" install.py` — 4 (Step1 checks, Step3 install, Update, Uninstall second confirm)
- `compose down` in UpdateScreen — appears ONLY in comments, not in executable code

## Self-Check: PASSED

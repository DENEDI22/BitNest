---
phase: 10-linux-x86-64-installer
plan: "02"
subsystem: installer
tags: [installer, python, textual, tui, docker-compose]

requires:
  - phase: 10-01
    provides: installer-core-logic (validation, preflight, secrets, state file)
provides:
  - textual-tui-screens (MainMenuScreen, Step1PrerequisitesScreen, Step2ConfigurationScreen)
  - installer-app-shell (InstallerApp with CSS, navigation, entry point)
  - step-indicator-widget
affects:
  - 10-03 (Step3InstallingScreen, Step4DoneScreen, UpdateScreen, UninstallScreen)

tech-stack:
  added:
    - textual (TUI framework, auto-installed via _ensure_textual() on __main__)
  patterns:
    - Textual Screen subclasses with compose()/on_mount() lifecycle pattern
    - "@work(exclusive=True) worker for async prerequisite checks"
    - Stub fallback base classes (_Stub) so module imports without textual for unit tests
    - Validation triggered only on Next Step button press (never on keystroke)
    - _ensure_textual() called at __main__ entry point to avoid breaking test imports

key-files:
  created: []
  modified:
    - installers/linux-x86_64/install.py
    - installers/linux-x86_64/.gitignore

key-decisions:
  - "Deferred textual auto-install to __main__ entry point (_ensure_textual) to avoid breaking unit test imports on externally-managed Python (Arch Linux)"
  - "Added _Stub fallback base classes so TUI class definitions parse without raising NameError when textual is absent"
  - "Back navigation from Step1 uses pop_screen() returning to MainMenu; from Step2 uses pop_screen() returning to Step1"
  - "Default port pre-populated with next available port (5001/3001) when 5000/3000 are in use per conflict detection in Step1"

patterns-established:
  - "Textual Screen classes use type: ignore[misc] comment on class line due to stub fallback"
  - "@work(exclusive=True) for all blocking I/O in screen workers; call_from_thread() for UI updates"

requirements-completed: [INST-01, INST-02, INST-04, INST-07]

duration: 5min
completed: "2026-03-26"
---

# Phase 10 Plan 02: Textual TUI Screens Summary

**Textual TUI with InstallerApp shell, StepIndicator widget, MainMenuScreen, Step1PrerequisitesScreen (async @work checks), and Step2ConfigurationScreen (inline validation on Next Step press) wired to Plan 01 core logic.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-26T10:53:15Z
- **Completed:** 2026-03-26T10:57:30Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- InstallerApp with full Textual CSS (dark navy color scheme matching UI-SPEC: #1a1a2e, #16213e, #e94560 accent)
- StepIndicator widget rendering 4-step progress bar with active step highlighted in accent
- MainMenuScreen with 3-item ListView pre-highlighting Install or Update based on state file presence
- Step1PrerequisitesScreen running all 5 prerequisite checks in @work async worker: Docker Engine, Compose V2, Port 5000, Port 3000, disk space
- Step2ConfigurationScreen with 5 labeled inputs (install dir, api/frontend ports, admin username/password) and inline validation on Next Step
- All 67 existing unit tests remain green after import-time textual guard added

## Task Commits

1. **Task 1: Textual CSS + StepIndicator + MainMenuScreen** - `b9fbad2` (feat)
2. **Task 2: Step1PrerequisitesScreen + Step2ConfigurationScreen** - `8f20915` (feat)

## Files Created/Modified

- `installers/linux-x86_64/install.py` - Full TUI implementation added after `# --- TUI --- #` marker
- `installers/linux-x86_64/.gitignore` - Added to exclude Python __pycache__ artifacts

## Decisions Made

- Deferred textual auto-install bootstrap to `__main__` entry point only — running pip at module import time crashed unit tests on Arch Linux (externally-managed environment). Solution: wrap the bootstrap in `_ensure_textual()` called at `if __name__ == "__main__"`.
- Added `_Stub` fallback base class so `Binding("q", ...)`, `Label(...)` etc. in class bodies don't raise `TypeError: object() takes no arguments` when textual is absent.
- Back from Step1 → MainMenu uses `pop_screen()` (MainMenu was push_screen'd by app.on_mount, so pop returns to it cleanly).
- Step1 computes default port values for Step2 based on port conflict detection: if 5000 in conflict list, defaults to 5001.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Textual bootstrap broke unit test imports on Arch Linux**
- **Found during:** Task 2 verification (running pytest)
- **Issue:** Plan spec placed textual auto-install at module top level; on Arch Linux the pip install fails with "externally-managed-environment" error, crashing all test collection
- **Fix:** Moved bootstrap into `_ensure_textual()` function called only from `if __name__ == "__main__"`. Added `_Stub` fallback classes for textual base classes so TUI class definitions load cleanly without textual installed.
- **Files modified:** installers/linux-x86_64/install.py
- **Verification:** `python -m pytest installers/linux-x86_64/tests/ -x -q` → 67 passed
- **Committed in:** 8f20915 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug)
**Impact on plan:** Fix was necessary for test suite compatibility. The runtime behavior is identical — textual is still auto-installed when running `python install.py` directly. No scope creep.

## Issues Encountered

- Plan 01 files (install.py, test suite) were committed on `worktree-agent-a58ff904` branch, not present in this worktree. Used `git checkout worktree-agent-a58ff904 -- installers/...` to retrieve them.

## Known Stubs

- `UpdateScreen` — compose/button wired but no actual update logic (Plan 03)
- `UninstallScreen` — compose/button wired but no actual uninstall logic (Plan 03)
- `Step3InstallingScreen` — placeholder only, awaiting Plan 03 installation sequence

These stubs are navigation stubs — they prevent crashes on menu selection but do not implement the described functionality. The plan's goal (MainMenu, Step1, Step2 screens) is fully achieved; these stubs do not block the plan's success criteria.

## Next Phase Readiness

- Plan 03 can implement Step3InstallingScreen, Step4DoneScreen, UpdateScreen, and UninstallScreen — all routing is wired
- Config dict with keys `install_dir, api_port, frontend_port, admin_user, admin_pass, db_password, jwt_key, docker_missing` is passed from Step2 to Step3
- All validation functions from Plan 01 are being called correctly

---
*Phase: 10-linux-x86-64-installer*
*Completed: 2026-03-26*

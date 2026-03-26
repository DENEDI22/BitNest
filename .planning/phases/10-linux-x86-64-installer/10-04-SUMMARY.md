---
plan: 10-04
phase: 10-linux-x86-64-installer
status: complete
completed: 2026-03-26
---

## Summary

Human-verified the complete BitNest Linux x86_64 installer end-to-end on a real terminal.

## What Was Built

No new code in this plan — this was a human verification checkpoint. One fix was made during verification: added a root privilege guard at the entry point so the installer exits immediately with a clear message if not run with `sudo`.

## Tasks Completed

| Task | Status |
|------|--------|
| 1. End-to-end installer verification on real terminal | ✓ Complete |

## Verification Results

All 4 test scenarios approved by human:

- **Test 1 (Fresh install):** Main menu, Step 1–4, Docker pull, health poll, browser login — all pass
- **Test 2 (Update flow):** State detection, pull streaming, restart without `docker compose down` — pass
- **Test 3 (Uninstall flow):** Two-confirmation flow, data preservation and deletion paths — pass
- **Test 4 (Navigation):** Back/Next between screens, Q to quit — pass

Automated tests: 67/67 passed.

## Key Files

### Modified
- `installers/linux-x86_64/install.py` — added `os.geteuid() != 0` guard at entry point

## Decisions

- Installer must be invoked with `sudo`; exits with a clear error message otherwise. The internal `sudo` prefixes on individual commands are retained for compatibility when run as root.

## Self-Check: PASSED

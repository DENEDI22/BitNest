---
phase: quick
plan: 260324-ccs
subsystem: frontend
tags: [bugfix, ui, upload-slot, form]
dependency_graph:
  requires: []
  provides: [upload-slot-preset-sync]
  affects: [FrontEnd/links.js]
tech_stack:
  added: []
  patterns: [datetime-local formatting, preset-to-input sync]
key_files:
  modified:
    - FrontEnd/links.js
decisions:
  - Format selectedSlotExpiry using local date components (getFullYear/getMonth/etc) to produce YYYY-MM-DDTHH:mm for datetime-local input
metrics:
  duration: ~2min
  completed: "2026-03-24"
---

# Quick Task 260324-ccs: Fix Expiry Date Field Not Populating When Preset Buttons Clicked

**One-liner:** Fixed two preset click handlers in links.js that were clearing custom inputs instead of populating them with the selected preset values.

## What Was Done

Two bugs were fixed in `FrontEnd/links.js` in the upload slot creation form:

1. **Expiry preset handler** (line 217): Was setting `uploadSlotCustomExpiry.value = ""`, which cleared the field instead of showing the selected date. Fixed to format `selectedSlotExpiry` (a Date object computed just above) into `YYYY-MM-DDTHH:mm` local datetime string using `getFullYear`, `getMonth`, `getDate`, `getHours`, `getMinutes`.

2. **Count preset handler** (line 234): Was setting `uploadSlotCustomCount.value = ""`, which cleared the count field. Fixed to set it to `btn.dataset.count` (the same integer value already parsed into `selectedSlotCount`).

No other behavior was changed. The custom input `change`/`input` event handlers that allow manual override remain as-is.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Sync preset buttons to custom input fields | 6047ed0 | FrontEnd/links.js |

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Self-Check: PASSED

- FrontEnd/links.js modified with both fixes present
- Commit 6047ed0 exists
- `getFullYear` and datetime-local format string confirmed on line 220
- `btn.dataset.count` assignment confirmed on line 237

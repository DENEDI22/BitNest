---
phase: 9
slug: sharepoint-dropbox-upload
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-20
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET) |
| **Config file** | `BitNest.Tests/BitNest.Tests.csproj` |
| **Quick run command** | `dotnet test BitNest.Tests/ --no-build -q` |
| **Full suite command** | `dotnet test BitNest.Tests/ -q` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test BitNest.Tests/ --no-build -q`
- **After every plan wave:** Run `dotnet test BitNest.Tests/ -q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 09-01-01 | 01 | 1 | SHRP-03 | migration | `dotnet ef migrations list` | ✅ | ⬜ pending |
| 09-01-02 | 01 | 1 | SHRP-03 | unit | `dotnet test BitNest.Tests/ -q --filter "SharepointLinkService"` | ❌ W0 | ⬜ pending |
| 09-01-03 | 01 | 2 | SHRP-03 | integration | `dotnet test BitNest.Tests/ -q --filter "PublicUpload"` | ❌ W0 | ⬜ pending |
| 09-02-01 | 02 | 1 | SHRP-03 | manual | n/a — browser upload flow | n/a | ⬜ pending |
| 09-02-02 | 02 | 1 | SHRP-03 | manual | n/a — slot creation UI | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `BitNest.Tests/SharepointLinkServiceUploadTests.cs` — stubs for `ValidateAndReserveUploadSlotAsync` (capacity check, expiry, revoke, linktype guard)
- [ ] `BitNest.Tests/PublicUploadControllerTests.cs` — stubs for upload endpoint (valid slot, expired slot, full slot, wrong link type)

*Existing test infrastructure (xUnit + TestServer + in-memory EF) covers all other phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| upload.html renders full context card (owner, expiry, description) | SHRP-03 | Browser rendering | Visit `/upload?token={valid}`, confirm all fields visible |
| Slot-full message distinct from expired message | SHRP-03 | Visual distinction requires browser | Visit slot after file limit reached; confirm "slot full" not "expired" copy |
| Form resets after successful upload (allows re-upload) | SHRP-03 | Interactive flow | Upload a file, confirm form resets, upload second file |
| Upload slot creation UI on #links page | SHRP-03 | Browser DOM | Click "New upload slot", fill form, confirm slot appears in list with Upload badge |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

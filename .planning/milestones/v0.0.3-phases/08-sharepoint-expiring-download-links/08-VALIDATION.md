---
phase: 08
slug: sharepoint-expiring-download-links
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 08 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (existing BitNest test project) |
| **Config file** | `BitNest.Tests/BitNest.Tests.csproj` (existing) |
| **Quick run command** | `dotnet test --filter "Category=SharepointLinks" --no-build` |
| **Full suite command** | `dotnet test --filter "FullyQualifiedName~Sharepoint"` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=SharepointLinks" --no-build`
- **After every plan wave:** Run `dotnet test --filter "FullyQualifiedName~Sharepoint"`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | SHRP-01,SHRP-04 | integration | `dotnet test --filter "SharepointLinkServiceTests"` | ⬜ W0 | ⬜ pending |
| 08-01-02 | 01 | 1 | SHRP-01,SHRP-02,SHRP-04 | integration | `dotnet test --filter "SharepointControllerTests"` | ⬜ W0 | ⬜ pending |
| 08-01-03 | 01 | 1 | SHRP-02,SHRP-04 | integration | `dotnet test --filter "PublicShareControllerTests"` | ⬜ W0 | ⬜ pending |
| 08-02-01 | 02 | 2 | SHRP-05 | e2e-manual | Manual verification (UI) | N/A | ⬜ pending |
| 08-02-02 | 02 | 2 | SHRP-01,SHRP-05 | e2e-manual | Manual verification (UI) | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `BitNest.Tests/Services/SharepointLinkServiceTests.cs` — stubs for token generation, expiry validation, revocation
- [ ] `BitNest.Tests/Controllers/SharepointControllerTests.cs` — stubs for authenticated link create/list/revoke
- [ ] `BitNest.Tests/Controllers/PublicShareControllerTests.cs` — stubs for public metadata/download endpoints with expiry/revocation checks

*Note: Existing test infrastructure (`BitNest.Tests/` project with xUnit and TestServer setup) covers all framework needs. Wave 0 only needs test file scaffolds.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Copy-to-clipboard feels instant | SHRP-01 | UI interaction quality | 1. Create link, 2. Click copy button, 3. Verify clipboard contains URL within 200ms |
| Download page branding matches app theme | SHRP-02 | Visual design consistency | 1. Visit `/share/{token}`, 2. Verify colors/fonts match main app |
| Expired link error page is clear | SHRP-04 | UX messaging quality | 1. Visit expired link, 2. Verify message clearly states "link expired or no longer valid" |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

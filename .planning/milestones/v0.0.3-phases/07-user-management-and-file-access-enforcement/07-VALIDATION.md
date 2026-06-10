---
phase: 07
slug: user-management-and-file-access-enforcement
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-19
---

# Phase 07 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (.NET 9) |
| **Config file** | `BitNest.Tests/BitNest.Tests.csproj` |
| **Quick run command** | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~(AccessControl|AdminUser|FrontendAccess)"` |
| **Full suite command** | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj` |
| **Estimated runtime** | ~90 seconds |

---

## Sampling Rate

- **After every task commit:** Run `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~(AccessControl|AdminUser|FrontendAccess)"`
- **After every plan wave:** Run `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | ACCS-05 | integration | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~AccessControl"` | ✅ | ⬜ pending |
| 07-01-02 | 01 | 1 | USER-02 | integration | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~AdminUser"` | ✅ | ⬜ pending |
| 07-02-01 | 02 | 2 | ACCS-01, ACCS-02, ACCS-03 | integration | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~AccessControl"` | ✅ | ⬜ pending |
| 07-02-02 | 02 | 2 | USER-01, USER-03 | integration | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~AdminUser"` | ✅ | ⬜ pending |
| 07-03-01 | 03 | 3 | ACCS-04 | source-contract | `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~FrontendAccess"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Admin route UX and error-page copy render as intended | USER-01, ACCS-04 | Visual QA of static frontend layout/messages | Run app, sign in as admin/non-admin, open `/admin`, force unauthorized file action, confirm expected pages and back actions |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

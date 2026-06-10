---
phase: 06
slug: identity-and-session-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 06 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit via `dotnet test` (Wave 0 scaffold required) |
| **Config file** | none — Wave 0 installs |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Auth"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Auth"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | AUTH-01 | integration | `dotnet test --filter "FullyQualifiedName~AuthSignup"` | ❌ W0 | ⬜ pending |
| 06-01-02 | 01 | 1 | AUTH-02 | integration | `dotnet test --filter "FullyQualifiedName~AuthLogin"` | ❌ W0 | ⬜ pending |
| 06-02-01 | 02 | 2 | AUTH-04 | integration | `dotnet test --filter "FullyQualifiedName~AuthRefresh"` | ❌ W0 | ⬜ pending |
| 06-02-02 | 02 | 2 | AUTH-03 | integration | `dotnet test --filter "FullyQualifiedName~AuthLogout"` | ❌ W0 | ⬜ pending |
| 06-03-01 | 03 | 3 | AUTH-01, AUTH-02 | smoke | `dotnet test --filter "FullyQualifiedName~AuthFrontendFlow"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `BitNest.Tests/Auth/AuthSignupTests.cs` — signup validation and duplicate-username coverage
- [ ] `BitNest.Tests/Auth/AuthLoginTests.cs` — login success and invalid-credential cases
- [ ] `BitNest.Tests/Auth/AuthRefreshTests.cs` — refresh rotation and revoked-token rejection
- [ ] `BitNest.Tests/Auth/AuthLogoutTests.cs` — logout revocation behavior
- [ ] `BitNest.Tests/BitNest.Tests.csproj` — test project scaffold with ASP.NET Core integration test packages

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Auth-first screen replaces file UI during bootstrap | AUTH-01, AUTH-02 | DOM flow in static UI | Start app, clear cookies, open root URL, confirm sign-in/signup screen appears before file list |
| Logout confirmation then sign-in redirect | AUTH-03 | UX timing/message | Sign in, click logout, verify short confirmation then auth screen appears |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

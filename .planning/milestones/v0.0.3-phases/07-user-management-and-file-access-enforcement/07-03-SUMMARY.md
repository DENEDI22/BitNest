---
phase: 07-user-management-and-file-access-enforcement
plan: 03
subsystem: frontend
tags: [frontend, admin-ui, hash-routing, access-control, ux]
requires:
  - phase: 07-user-management-and-file-access-enforcement
    plan: 02
    provides: Admin endpoints, auth guards, storage authorization
provides:
  - Hash-based client-side routing (#admin, #files, #access-denied, #file-not-found)
  - Admin panel UI with user list, create, and disable flows
  - Access-denied view for non-admin direct #admin access
  - Unified file-404 view with session preservation
  - Role-aware admin link visibility from JWT isAdmin claim
affects: [phase-08, phase-09, admin-workflows, frontend-ux]
tech-stack:
  added:
    - Hash-based routing via history.pushState + hashchange listener
    - Admin panel view with user management
  patterns:
    - Hash routing for SPA views without a server router
    - applyMeData() centralizes /auth/me response consumption
    - authHeaders(baseHeaders) pattern for merging Content-Type + Authorization
key-files:
  created:
    - BitNest.Tests/Auth/FrontendAccessFlowTests.cs
  modified:
    - FrontEnd/index.html (admin, access-denied, file-404 view containers; header nav)
    - FrontEnd/main.js (hash routing, admin API handlers, role gating)
    - FrontEnd/style.css (admin panel, error view, header nav styles)
key-decisions:
  - "Hash routing (#admin, #files) used instead of path-based routing — SPA served from static file server has no server-side routing"
  - "Admin and Sign out buttons placed in the brand header bar, not inside the files card"
  - "JWT must include 'admin' claim for controller IsAdmin() check to work"
  - "authHeaders(baseHeaders) pattern prevents silent Authorization header loss from spreading Headers objects"
  - "OwnerUserId made nullable in migration to avoid FK violation on pre-existing file rows"
  - "IsActive migration default changed from false to true to avoid silently disabling existing users"
requirements-completed: [USER-01, USER-03, ACCS-04]
duration: 90min
completed: 2026-03-19
---

# Phase 7 Plan 03: Frontend Admin UI & Access Control Flows

**Hash-routed admin panel, access-denied page, and unified file-404 view are live and browser-verified.**

## Performance

- **Duration:** ~90 min (including bug fixes)
- **Tasks:** 3 (2 automated + 1 human checkpoint)
- **Files modified:** 4
- **Commits:** 5

## Accomplishments

- **Task 1:** Frontend behavior contract tests for admin routing and unified 404 flow
  - `FrontendAccessFlowTests.cs` — 7 assertions covering admin visibility, 404 view, access-denied routing, "Back to files" presence, and `/admin/users` API usage
  - `AuthFrontendFlowTests.cs` extended with isAdmin consumption assertions

- **Task 2:** Admin panel, access-denied view, unified file-404 view implemented in frontend
  - Admin panel at `#admin` with user list, create user form, disable user action
  - Access-denied view for non-admin `#admin` access with "Back to files"
  - Unified file-404 view for unauthorized/missing file actions, session preserved
  - Admin + Sign out buttons in header bar, visible only when authenticated
  - Admin link hidden for non-admin users

- **Task 3:** Human verification confirmed all 5 flows in browser — approved

## Task Commits

1. `e17ff34` — feat: frontend admin UI and access control flows
2. `5dc1562` — fix: nullable OwnerUserId migration (FK violation on existing rows)
3. `a6d6dc2` — fix: header nav placement + Create User form class mismatch
4. `6b80e51` — fix: admin claim in JWT + Authorization header on create-user request
5. `659bd80` — fix: hash-based routing for admin/error views

## Deviations from Plan

### Auto-fixed Issues

**1. Migration FK violation on existing data**
- `OwnerUserId NOT NULL DEFAULT 0` failed FK constraint — no User with ID 0
- Fixed: `OwnerUserId` made nullable; legacy files have no owner (shown to all)
- Also fixed: `IsActive` migration default `false` → `true` (would have disabled all existing users)

**2. JWT missing `admin` claim**
- `JwtTokenService.CreateAccessToken` only included NameIdentifier and Name
- `AdminUsersController.IsAdmin()` always returned false — every user got 403
- Fixed: added `new Claim("admin", user.IsAdmin ? "true" : "false")` to token

**3. Authorization header silently dropped on create-user request**
- `...authHeaders()` spread of a `Headers` object into a plain object literal produces nothing
- Fixed: `authHeaders({ "Content-Type": "application/json" })` passes base headers into the helper

**4. Create User button did nothing**
- Form had class `hidden`; `setViewVisible()` toggles `view-hidden`/`view-visible`
- Fixed: changed form class to `view-hidden`

**5. Hash routing replacing pathname routing**
- App is a static SPA — `window.location.pathname` never changes from `/`
- Fixed: hash routing (`#admin`, `#files`, `#access-denied`, `#file-not-found`) with `history.pushState` and `hashchange` listener

## Self-Check: PASSED

---
*Phase: 07-user-management-and-file-access-enforcement*
*Completed: 2026-03-19*

# Phase 7 Research: User Management and File Access Enforcement

**Date:** 2026-03-19
**Scope:** Plan-ready technical guidance for `USER-01..03` and `ACCS-01..05`

## Research Outcome

Phase 7 should extend the existing auth-enabled backend/frontend with admin-only user management and owner/grant authorization for storage list, download, and delete operations. Authorization-denied file access should collapse to one 404 behavior to avoid resource-existence leaks.

## Constraints and Inputs

- Locked decisions from `07-CONTEXT.md`:
  - Unauthorized files are hidden from normal file list.
  - Direct unauthorized file access and missing/deleted files use a unified HTTP 404 behavior.
  - Frontend requires a dedicated unified file 404 page with a primary "Back to files" action.
  - Admin route is `/admin`; non-admin users do not see navigation entry and direct access shows dedicated access-denied page.
  - If admin role is revoked during session, next admin action should force re-login.
  - Admin can create users (username + password) and disable users.
  - Disabling a user immediately invalidates active sessions.
  - Grant ownership policy: only file owner can grant/revoke; grants are per-file for this phase.
  - Granted users have full access for that file (list visibility, download, delete).
- Existing baseline from phase 6:
  - JWT access token auth and `/auth/me` identity endpoint exist.
  - `User` and `RefreshSession` entities already exist.
  - Frontend has auth-first shell and protected file actions in `FrontEnd/main.js`.

## Recommended Technical Approach

### 1) Persistence model updates

- Extend `User` entity with admin + account status:
  - `IsAdmin` (bool, default false)
  - `IsActive` (bool, default true)
  - `LastSignInAt` (nullable datetime)
- Extend `FileMetadata` with owner relationship:
  - `OwnerUserId` (int FK to `Users`)
  - `OwnerUser` navigation
- Add `FileGrant` join entity:
  - `Id`, `FileId`, `GrantedUserId`, `GrantedByUserId`, `CreatedAt`
  - Unique index on (`FileId`, `GrantedUserId`)
  - Cascade delete from file to grants
- Extend `RefreshSession` checks to support account disable behavior:
  - auth flows should reject sessions where `User.IsActive == false`
  - disable endpoint should revoke all active sessions for that user.

### 2) Authorization service boundaries

- Keep controller thin and move policy checks into service-layer methods.
- Add reusable authorization helper in storage service or dedicated service:
  - `CanAccessFile(userId, fileId)` true for owner or explicit grant
  - `CanManageFileGrants(userId, fileId)` true for owner only
- List query should include only:
  - files where `OwnerUserId == currentUserId`, or
  - files with `FileGrant.GrantedUserId == currentUserId`
  - plus existing not-deleted and uploaded filters.
- Download/delete paths must resolve authorization before loading/streaming/deleting and return unified 404 on deny.

### 3) Admin API surface

- Add admin-only controller (for example `AdminUsersController`) with `[Authorize]` and explicit admin check:
  - `GET /admin/users` => username, status, created date, last sign-in
  - `POST /admin/users` => create username+password user
  - `POST /admin/users/{id}/disable` => set `IsActive=false`, revoke sessions
- Add stable access-denied handling:
  - non-admin => 403 for admin API calls
  - frontend route handler maps this to access-denied page.

### 4) Auth service alignment

- Login and refresh should reject disabled accounts with stable auth error shape.
- Successful login updates `LastSignInAt`.
- `/auth/me` should include role/admin status so frontend can hide/show admin nav without extra calls.

### 5) Frontend behavior and routing

- Keep plain-JS routing with path inspection (`window.location.pathname`) and view-state toggles.
- Add views:
  - `/admin`: user management page (admin only)
  - unified file 404 page for missing/deleted/unauthorized file outcomes
  - admin access-denied page for non-admin `/admin` access
- File list rendering:
  - consume backend-filtered list for visibility
  - per-row controls include grant management action for owner
  - show disabled Delete when file visible but delete not allowed.
- Admin navigation:
  - render only when `/auth/me` indicates admin
  - for revoked admin role mid-session, admin API 401/403 should return user to sign-in per locked decision.

## Standard Stack and Patterns

- Stay on ASP.NET Core + EF Core patterns used in phase 6.
- Continue `{ code, message }` auth/admin error DTO shape for consistency.
- Continue xUnit + TestServer + EF InMemory integration tests for endpoint contracts.
- Keep frontend checks in `BitNest.Tests/Auth/*Frontend*Tests.cs` style for deterministic script-level behavior validation.

## Common Pitfalls to Avoid

- Returning 403 for unauthorized file resource (leaks existence); use unified 404 for file targets.
- Forgetting to revoke refresh sessions when disabling user.
- Grant query bugs that duplicate file rows (use distinct or grouped projection).
- Admin UI entry leakage (showing `/admin` controls to non-admin users).
- Grant management without owner check.

## Plan Implications

- Keep roadmap split into three plans:
  1. Persistence and contracts for role/status/ownership/grants.
  2. Backend authorization enforcement and admin APIs.
  3. Frontend admin route, grant controls, and unified error/access views.
- Requirement coverage mapping:
  - `ACCS-05` in plan 01 (grant model)
  - `ACCS-01..03` + `USER-02` in plan 02
  - `USER-01`, `USER-03`, `ACCS-04` in plan 03

## Validation Architecture

- Existing `BitNest.Tests` infrastructure is sufficient; no Wave 0 test-framework bootstrap needed.
- Add focused backend integration tests for admin and storage authorization contracts:
  - `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~AccessControl"`
  - `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~AdminUser"`
- Add frontend script assertions for route gating and error-page flows:
  - `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj --filter "FullyQualifiedName~FrontendAccess"`
- Full regression per plan wave:
  - `~/.dotnet/dotnet test BitNest.Tests/BitNest.Tests.csproj`

## RESEARCH COMPLETE

Phase 7 has sufficient implementation guidance for planning.

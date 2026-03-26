---
phase: 07-user-management-and-file-access-enforcement
plan: 01
subsystem: database
tags: [ef-core, migrations, authorization, admin, testing]
requires:
  - phase: 06-identity-and-session-foundation
    provides: Auth user/session entities and endpoint test host patterns
provides:
  - User admin/status persistence fields for downstream auth and admin APIs
  - File ownership and per-file grant persistence model with unique grant constraint
  - Focused phase 7 schema contract tests for owner/grant and disable-state requirements
affects: [07-02, 07-03, auth, storage, admin]
tech-stack:
  added: []
  patterns:
    - Reflection-based schema contract tests for RED-first model evolution
    - EF relationship + index configuration in AppDbContext before API wiring
key-files:
  created:
    - BitNest.Tests/Auth/AdminUserEndpointTests.cs
    - BitNest.Tests/Storage/AccessControlTests.cs
    - BitNest/Models/FileGrant.cs
    - BitNest/Migrations/20260319021858_Phase7AccessFoundation.cs
    - BitNest/Migrations/20260319021858_Phase7AccessFoundation.Designer.cs
  modified:
    - BitNest/Models/User.cs
    - BitNest/Models/FileMetadata.cs
    - BitNest/Data/AppDbContext.cs
    - BitNest/Migrations/AppDbContextModelSnapshot.cs
key-decisions:
  - "Modeled grants as a dedicated FileGrant entity with unique index on (FileId, GrantedUserId) to prevent duplicate grants."
  - "Configured grant user foreign keys with Restrict delete behavior to avoid accidental user-chain deletions."
patterns-established:
  - "Schema contracts first: write failing tests for required properties/indexes before model migration changes."
requirements-completed: [USER-02, ACCS-05]
duration: 22min
completed: 2026-03-19
---

# Phase 7 Plan 01: Access Foundation Summary

**User admin/status flags and owner/grant authorization schema now exist with migration artifacts and contract tests enforcing these persistence guarantees.**

## Performance

- **Duration:** 22 min
- **Started:** 2026-03-19T02:58:00Z
- **Completed:** 2026-03-19T03:20:00Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- Added RED integration-style schema tests for user disable/admin properties and owner+grant file setup contracts.
- Implemented `User`, `FileMetadata`, and new `FileGrant` persistence models with required relationship navigation properties.
- Added EF model configuration and `Phase7AccessFoundation` migration/snapshot updates including unique grant index.

## Task Commits

1. **Task 1: Add failing persistence contract tests for role/status and grant schema** - `9de2191` (test)
2. **Task 2: Implement role/status/ownership/grant models and migration** - `4420c5e` (feat)

## Files Created/Modified
- `BitNest.Tests/Auth/AdminUserEndpointTests.cs` - RED schema contract tests for admin/active/last-sign-in user fields.
- `BitNest.Tests/Storage/AccessControlTests.cs` - RED schema contract tests for owner/grant setup and unique grant constraints.
- `BitNest/Models/User.cs` - admin/status fields and ownership/grant navigation collections.
- `BitNest/Models/FileMetadata.cs` - owner foreign key and grant navigation support.
- `BitNest/Models/FileGrant.cs` - per-file grant entity with granted/granted-by references.
- `BitNest/Data/AppDbContext.cs` - FileGrant DbSet and owner/grant relationship + index configuration.
- `BitNest/Migrations/20260319021858_Phase7AccessFoundation.cs` - migration adding owner/grant and user status schema updates.
- `BitNest/Migrations/20260319021858_Phase7AccessFoundation.Designer.cs` - migration model designer.
- `BitNest/Migrations/AppDbContextModelSnapshot.cs` - updated EF snapshot for phase 7 schema.

## Decisions Made
- Kept `OwnerUserId` as required field in model so owner checks are first-class for downstream authorization.
- Used EF metadata assertions in tests for index-level guarantees because InMemory provider does not enforce relational constraints.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed invalid test filter expression for vstest**
- **Found during:** Task 1 (Add failing persistence contract tests for role/status and grant schema)
- **Issue:** Plan command `FullyQualifiedName~(AdminUserEndpointTests|AccessControlTests)` is not valid for vstest filter syntax and executed zero tests.
- **Fix:** Switched verification command to `FullyQualifiedName~AdminUserEndpointTests|FullyQualifiedName~AccessControlTests`.
- **Files modified:** None (execution command only)
- **Verification:** Focused test run executed 3 tests and produced expected RED failures.
- **Committed in:** `9de2191` (part of task flow)

**2. [Rule 3 - Blocking] Resolved `dotnet-ef` runtime invocation mismatch**
- **Found during:** Task 2 (Implement role/status/ownership/grant models and migration)
- **Issue:** Direct `~/.dotnet/dotnet ef` and default tool invocation could not run migrations in current runtime path.
- **Fix:** Ran `dotnet-ef` with `DOTNET_ROOT` and `PATH` set to local `~/.dotnet` locations.
- **Files modified:** `BitNest/Migrations/20260319021858_Phase7AccessFoundation.cs`, `BitNest/Migrations/20260319021858_Phase7AccessFoundation.Designer.cs`, `BitNest/Migrations/AppDbContextModelSnapshot.cs`
- **Verification:** Migration generated successfully and focused tests passed.
- **Committed in:** `4420c5e` (part of task commit)

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both fixes were execution-environment or command-format corrections; planned scope and outputs remained unchanged.

## Issues Encountered
- Existing repo warning noise (nullable and assembly-version conflicts) persists but did not block focused phase 7 task verification.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Backend now has the persistence primitives required for admin APIs and owner/grant authorization logic in plan 07-02.
- Frontend/admin UX work in plan 07-03 can now rely on explicit user role/status and grant-backed access model semantics.

## Self-Check: PASSED

---
*Phase: 07-user-management-and-file-access-enforcement*
*Completed: 2026-03-19*

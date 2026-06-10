# Phase 8: Sharepoint Expiring Download Links - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Allow authenticated users to generate time-limited, publicly-accessible download links for specific files. Public users (no login required) can download files via those links until they expire or are revoked. Authenticated users can view and revoke their active links from a dedicated management view. Phase 9 (dropbox upload via sharepoint links) is out of scope here.

</domain>

<decisions>
## Implementation Decisions

### Link creation flow
- Link creation is triggered from a per-file action in the file list row (a "Share" button/action on each row) — consistent with Phase 7's per-file grant management pattern.
- Expiration options: presets (1 hour, 24 hours, 7 days, 30 days) plus a custom date/time option for users who need a specific expiry.
- After creation, the shareable URL is shown inline with a copy-to-clipboard button — no navigation away, user stays in the files view.
- Multiple active links per file are allowed simultaneously — each has its own token and expiry.

### Active links management UI
- Accessible via a dedicated `#links` route in the header nav — consistent with Phase 7's hash routing pattern (`#admin`, `#files`).
- All authenticated users can access it (not admin-only).
- Each active link row shows: file name, creation date, expiry time, copy URL button, revoke button.
- Users can revoke a link early via a revoke button on each row.
- Empty state: message guiding the user to create a link from the Files view — e.g., "No active links — share a file from the Files view to create one."

### Public download UX
- Fully public — no login required. The token in the URL is the sole credential.
- Public users land on a minimal branded download page showing: file name, file size, expiry time, and a prominent Download button.
- Download is triggered by the button, not automatically on page load.
- If download fails (network/server error), show an inline error with a retry button — do not navigate away.

### Expired/invalid link behavior
- Distinct branded error page (not the Phase 7 unified file-404 page) — the messaging must clearly distinguish "link expired/revoked" from "file not found/unauthorized".
- Message direction: "This link has expired or is no longer valid." — no action offered (public user has no account to take action with).
- Backend returns 404 or 410 for invalid/expired tokens; frontend maps this to the branded expired-link page.

### Claude's Discretion
- Exact styling and layout of the public download page and expired-link error page (consistent with existing app theme).
- Token format and length for sharepoint links (reuse `RandomNumberGenerator.GetBytes(64)` pattern from `JwtTokenService`).
- Database model design for sharepoint link entity (token hash storage, revocation mechanism).
- Exact text polish on all messages beyond the stated intent.

</decisions>

<specifics>
## Specific Ideas

- Phase 7 established: "unified 404 for missing/unauthorized files, distinct access-denied for non-admin /admin access" — the expired-link page is a third distinct error type, not reusing either of those.
- Copy-to-clipboard after link creation should feel instant and inline — no modal or separate page.
- The `#links` nav entry should appear in the header nav alongside the existing "Admin" (admin-only) and "Sign out" buttons.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope and requirements
- `.planning/ROADMAP.md` — Phase 8 goal, dependency chain, success criteria (SHRP-01, SHRP-02, SHRP-04, SHRP-05), and plan outline.
- `.planning/REQUIREMENTS.md` — Authoritative requirement IDs SHRP-01, SHRP-02, SHRP-04, SHRP-05 for this phase.
- `.planning/PROJECT.md` — Milestone constraints, stack constraints, active feature boundary.

### Prior locked context
- `.planning/phases/07-user-management-and-file-access-enforcement/07-CONTEXT.md` — Established hash routing pattern, unified 404 behavior, per-file action row pattern, and header nav structure that Phase 8 extends.
- `.planning/phases/06-identity-and-session-foundation/06-CONTEXT.md` — Auth/session behavior and JWT token patterns that Phase 8 builds on.
- `.planning/STATE.md` — Accumulated phase decisions and current project continuity notes.

### Codebase baseline and conventions
- `.planning/codebase/STRUCTURE.md` — Key file locations and where phase 8 integrations should land.
- `.planning/codebase/CONVENTIONS.md` — Controller/service/frontend patterns to preserve.
- `.planning/codebase/STACK.md` — Stack/runtime constraints.
- `.planning/codebase/INTEGRATIONS.md` — API/frontend/proxy integration context.

No external ADR/spec documents were referenced during discussion; requirements are fully captured in the docs above plus decisions in this context.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BitNest/Services/JwtTokenService.cs` — `RandomNumberGenerator.GetBytes(64)` and hash pattern (`HashRefreshSecret`) directly reusable for sharepoint token generation and storage.
- `FrontEnd/main.js` — `authHeaders()`, `setViewVisible()`, `routeToHash()`, `hashchange` listener, and `show404View()` patterns all reusable for the `#links` route and public download page.
- `FrontEnd/index.html` — Header nav (`#headerNav`) where the "Links" nav button can be added alongside existing "Admin" and "Sign out" buttons.
- `BitNest/Controllers/StorageController.cs` — `GetDownloadStreamAsync` is the existing download path; sharepoint download will need its own controller action or a parallel path.
- `BitNest/Data/AppDbContext.cs` — Central EF config point for the new sharepoint link entity.

### Established Patterns
- **Token storage**: Hash the token before persisting (same as `RefreshSession.TokenHash`) — never store raw tokens in DB.
- **Thin controller + service delegation**: All new sharepoint endpoints should follow the same pattern as `AdminUsersController` and `StorageController`.
- **Hash routing**: New `#links` view follows `#admin`/`#files`/`#access-denied` pattern — `routeToHash()` handles dispatch.
- **Per-file action rows**: File list already has per-row action controls (download, delete) — "Share" button slots into this row.
- **Unified error pages**: Phase 7 established `#file-not-found` and `#access-denied` as distinct named views — expired-link page is a third distinct view (`#link-expired` or served at the public link URL).

### Integration Points
- New `SharepointLink` entity connecting `FileMetadata` and `User` (owner), with hashed token, expiry, created-at, and revoked-at fields.
- New controller for sharepoint operations: create link (authenticated), list active links (authenticated), revoke link (authenticated), and public download endpoint (unauthenticated — separate from `StorageController`).
- Frontend: "Share" button per file row in `filesView`; `#links` view in app shell; public download landing page served at a separate URL path (e.g., `/share/{token}` routed by the frontend server or API).
- Header nav: "Links" button added to `#headerNav`, visible for all authenticated users (not admin-only).

</code_context>

<deferred>
## Deferred Ideas

- Download count tracking per sharepoint link — noted but not in Phase 8 scope. Add to backlog.
- Phase 9: Public upload via sharepoint links (dropbox flow) — separate phase, already in roadmap.

</deferred>

---

*Phase: 08-sharepoint-expiring-download-links*
*Context gathered: 2026-03-19*

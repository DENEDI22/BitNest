# Phase 7: User Management and File Access Enforcement - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver admin user-management controls and enforce owner/grant-based authorization for file list, download, and delete flows in existing BitNest backend and frontend. This phase clarifies access and admin behavior within current file workflows; sharepoint link capabilities remain in later phases.

</domain>

<decisions>
## Implementation Decisions

### Access visibility and denial behavior
- Files a user is not authorized to access are hidden completely from the main list.
- For direct access attempts (download/delete link) and missing resources, use one unified not-found behavior to avoid revealing file existence details.
- Backend response should be HTTP 404 with unified messaging for deleted, never-existing, or unauthorized file targets.
- Add a dedicated frontend error page for this unified 404 case, including a primary "Back to files" action.
- If access disappears mid-session (stale link or permission change), route to the same unified 404 page.
- When returning from this 404 page, keep the user signed in (this is an access/resource issue, not an auth-session issue).
- If a file is visible but delete is not allowed, show Delete as disabled.

### Admin entry and route behavior
- Expose user-management via a separate admin route: `/admin`.
- Hide admin navigation/entry points completely for non-admin users.
- If a non-admin opens admin route directly, show an access-denied page (not the unified file 404 page).
- Access-denied page includes a primary "Back to files" action.
- Admin flow starts from the files app first; direct bookmark landing to `/admin` is not required.
- If admin privileges are revoked while on admin area, force re-login on the next admin action.

### User lifecycle controls
- Admin create-user flow requires username and password.
- After successful create, show inline success confirmation only.
- Disabling a user invalidates active sessions immediately.
- Admin user list includes username, account status, created date, and last sign-in.

### File grant interaction
- Only file owner can grant/revoke access.
- Grant scope is per-file only in this phase.
- Manage grants from a per-file action in the files UI.
- Granted users receive full authorized capabilities for that file in this phase: view metadata, download, and delete.

### Claude's Discretion
- Exact visual composition/styling for admin route and error pages.
- Exact text polish outside locked message intent.
- UI control variants for grant management (inline vs modal details) as long as per-file entry and owner-only policy are preserved.

</decisions>

<specifics>
## Specific Ideas

- Unified 404 message direction from user: "The file you are looking for is deleted, never existed or the owner didnt give you permission to download it" (wording can be polished while preserving intent).
- User explicitly wants a dedicated frontend page for unified file-not-found/unauthorized outcomes.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope and requirements
- `.planning/ROADMAP.md` — Phase 7 goal, dependency chain, and success criteria for admin controls plus owner/grant enforcement.
- `.planning/REQUIREMENTS.md` — authoritative requirement IDs `USER-01..03` and `ACCS-01..05` for this phase.
- `.planning/PROJECT.md` — milestone constraints, stack constraints, and active feature boundary.

### Prior locked context
- `.planning/phases/06-identity-and-session-foundation/06-CONTEXT.md` — established auth/session UX and `/auth/*` behavior that Phase 7 extends.
- `.planning/STATE.md` — accumulated phase decisions and current project continuity notes.

### Codebase baseline and conventions
- `.planning/codebase/STRUCTURE.md` — key file locations and where phase 7 integrations should land.
- `.planning/codebase/CONVENTIONS.md` — controller/service/frontend patterns to preserve for consistency.
- `.planning/codebase/STACK.md` — stack/runtime constraints that bound implementation choices.
- `.planning/codebase/INTEGRATIONS.md` — API/frontend/proxy integration context and runtime wiring.

No external ADR/spec documents were referenced during discussion; requirements are fully captured in the docs above plus decisions in this context.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FrontEnd/main.js`: existing auth-aware fetch/XHR wrappers and file action handlers where access-aware rendering and admin entry logic can be extended.
- `FrontEnd/index.html`: current app shell that can host admin navigation and route transitions.
- `BitNest/Controllers/StorageController.cs`: existing list/download/delete entry points to apply owner/grant authorization outcomes.
- `BitNest/Services/StorageService.cs`: existing file query and action logic to scope by owner/grants.
- `BitNest/Data/AppDbContext.cs`: central EF model configuration point for role/admin and file-grant persistence relationships.
- `BitNest/Models/User.cs`: user entity baseline for account status/admin attributes.

### Established Patterns
- Thin controller + service delegation pattern in backend should be kept for new admin/access endpoints.
- Frontend remains plain JS with imperative DOM updates and no framework router.
- Auth/session behavior already checks session validity before protected actions; Phase 7 should layer authorization behavior on top of this path.
- Existing file list uses per-row action controls, which is a natural integration point for grant management and action visibility/disable states.

### Integration Points
- Add authorization checks to `StorageController` flows for list/download/delete outcomes.
- Extend file metadata retrieval in `StorageService` to filter by owner/grants before returning list payload.
- Add admin user-management API surface parallel to existing auth/storage controllers.
- Extend frontend app shell to include `/admin` entry/route handling and access-denied pathway.
- Introduce unified file 404 page flow in frontend for missing/deleted/unauthorized file targets.

</code_context>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 07-user-management-and-file-access-enforcement*
*Context gathered: 2026-03-19*

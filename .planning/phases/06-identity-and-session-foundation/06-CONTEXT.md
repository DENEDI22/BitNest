# Phase 6: Identity and Session Foundation - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver authentication and session foundations for BitNest: signup, login, logout, refresh flow, and frontend auth entry points. This phase defines auth/session UX and API contract. User-management admin controls and file access enforcement remain in later phases.

</domain>

<decisions>
## Implementation Decisions

### Auth entry flow
- Use a dedicated auth-first screen at root URL for unauthenticated users.
- Keep self-signup enabled in this phase.
- After successful login, land directly on the existing upload/list/download files app screen.

### Session behavior
- Use short-lived access sessions (~15 minutes) with refresh flow.
- Include a "Remember me" option in frontend auth UI.
- If user logs out in one tab, other tabs should effectively log out on next protected action.
- Allow concurrent sessions across multiple devices/browsers.

### Credential rules
- Username format: simple handle style (lowercase normalized, letters/numbers/`._-`).
- Password policy: minimum 8 characters.
- Admin-created users keep admin-provided initial password (no forced first-login reset in this phase).
- Duplicate username attempts must return clear "username already taken" feedback.

### Auth errors and loading UX
- Invalid credentials show inline form error (not generic page failure).
- If refresh fails for active user, redirect to sign-in with clear short message.
- On app startup, block file UI with auth-loading gate until auth state resolves.
- On successful logout, show brief confirmation then return to sign-in screen.

### API-level endpoint contract
- Use `/auth/*` namespace for session and identity endpoints.
- Required Phase 6 endpoints: `/auth/signup`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me`.
- Logout endpoint must revoke refresh token server-side.
- Standardize auth errors to a stable JSON shape (`code` + `message`).

### Claude's Discretion
- Exact visual composition/styling details for auth forms and loading gate.
- Exact wording for non-critical success messages beyond required clarity.
- Internal endpoint handler organization and controller/service split as long as API contract remains intact.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone and phase scope
- `.planning/ROADMAP.md` — Phase 6 goal, dependencies, and success criteria.
- `.planning/PROJECT.md` — milestone goal, constraints, and current active requirements context.
- `.planning/REQUIREMENTS.md` — authoritative requirement IDs and traceability for `AUTH-01..AUTH-04`.

### Existing codebase baseline
- `.planning/codebase/ARCHITECTURE.md` — current backend/frontend boundaries and request flow.
- `.planning/codebase/STRUCTURE.md` — file and folder integration points for controllers/services/frontend assets.
- `.planning/codebase/CONVENTIONS.md` — established coding and API patterns to preserve.

No external ADR/spec files were provided; requirements are captured in the docs above and decisions in this context.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FrontEnd/index.html`: existing app shell/cards that can host auth-first screen state.
- `FrontEnd/main.js`: existing imperative DOM and API-call pattern to extend with auth/session state.
- `BitNest/Data/AppDbContext.cs`: existing EF Core context where user/session entities can be integrated.
- `BitNest/Program.cs`: middleware registration point to add authentication before authorization.

### Established Patterns
- Backend uses thin controllers + service classes (`StorageController` -> `StorageService`).
- Frontend uses plain JS with global state and direct DOM updates (no framework/router).
- Existing route style is controller-token based, but this phase locks `/auth/*` for new auth endpoints.

### Integration Points
- Add auth endpoints in new auth controller/service while preserving existing `StorageController` flow.
- Extend frontend startup flow in `FrontEnd/main.js` to resolve auth state before loading file list.
- Wire token/session handling into existing API call layer used for file list/upload/delete/download actions.

</code_context>

<specifics>
## Specific Ideas

- User explicitly wants frontend implementation access points included, not backend-only auth.
- User explicitly called out that post-login experience should immediately show file screen, with access filtering handled by later phase enforcement.

</specifics>

<deferred>
## Deferred Ideas

- Immediate cross-tab logout propagation (push-style) was not selected; current decision is enforcement on next action.
- File visibility enforcement details ("only files user is supposed to see") are captured for Phase 7 access-control implementation.

</deferred>

---

*Phase: 06-identity-and-session-foundation*
*Context gathered: 2026-03-19*

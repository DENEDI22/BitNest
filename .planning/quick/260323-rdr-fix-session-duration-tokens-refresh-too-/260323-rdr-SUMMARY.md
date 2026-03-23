---
phase: quick
plan: 260323-rdr
subsystem: auth
tags: [jwt, token-refresh, session, frontend]

# Dependency graph
requires:
  - phase: 06-auth
    provides: JWT access tokens with exp claim, refresh token rotation endpoint
provides:
  - Smart JWT expiry tracking in all three frontend pages (main, links, admin)
  - Bootstrap sequence that skips guaranteed-401 /auth/me on cold start
  - fetchWithAuth() helper for automatic retry-on-401 in links and admin pages
affects: [frontend auth, page navigation, token lifecycle]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Parse JWT exp claim client-side with atob() to track tokenExpiresAt"
    - "isAccessTokenValid() with 60s safety margin before expiry"
    - "Bootstrap goes straight to refresh instead of /auth/me -> 401 -> refresh -> /auth/me"
    - "fetchWithAuth() wrapper for automatic single retry-on-401"

key-files:
  created: []
  modified:
    - FrontEnd/main.js
    - FrontEnd/links.js
    - FrontEnd/admin.js

key-decisions:
  - "Skip /auth/me on cold start — access token is memory-only so it is always empty on page load; going straight to refresh eliminates guaranteed 401"
  - "Track tokenExpiresAt from JWT exp claim with 60s safety margin; skip all network calls when token is valid"
  - "loadFiles() retries once on 401 before bouncing to sign-in, matching the fetchWithAuth pattern"

patterns-established:
  - "parseJwtExpiry(token): base64url-decode JWT payload, return exp * 1000 in ms"
  - "fetchWithAuth(url, options): single refresh-retry on 401, redirect to index.html if still 401"

requirements-completed: [quick-fix]

# Metrics
duration: 4min
completed: 2026-03-23
---

# Quick Task 260323-rdr Summary

**Eliminated aggressive token refresh pattern causing 401s on page navigation: JWT expiry tracked client-side, bootstrap goes straight to refresh, all API calls use fetchWithAuth for transparent retry-on-401.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-03-23T18:45:49Z
- **Completed:** 2026-03-23T18:49:55Z
- **Tasks:** 2 of 2 (auto tasks complete; human-verify checkpoint pending)
- **Files modified:** 3

## Accomplishments
- main.js: Added `parseJwtExpiry()`, `isAccessTokenValid()`, `tokenExpiresAt` state tracking; `ensureAuthenticatedForAction()` now skips all network calls when token is valid; `bootstrapAuthGate()` goes straight to refresh on cold start; `loadFiles()` retries once on 401
- links.js: Same JWT expiry helpers + `fetchWithAuth()` wrapper; bootstrap skips guaranteed-fail `/auth/me`; `loadLinks()`, revoke button, and create slot use `fetchWithAuth`
- admin.js: Same JWT expiry helpers + `fetchWithAuth()` wrapper; bootstrap skips guaranteed-fail `/auth/me`; `loadAdminUsers()`, `submitCreateUser()`, `disableUser()` use `fetchWithAuth`

## Task Commits

1. **Task 1: Smart token lifecycle in main.js** - `ef08770` (fix)
2. **Task 2: Smart token lifecycle in links.js and admin.js** - `20348a4` (fix)

## Files Created/Modified
- `FrontEnd/main.js` - JWT expiry parsing, isAccessTokenValid(), rewritten ensureAuthenticatedForAction/bootstrapAuthGate/loadFiles
- `FrontEnd/links.js` - JWT expiry tracking, fetchWithAuth, rewritten bootstrap, updated all API calls
- `FrontEnd/admin.js` - JWT expiry tracking, fetchWithAuth, rewritten bootstrap, updated all API calls

## Decisions Made
- Bootstrap for links.js and admin.js redirects to `index.html` on auth failure (no inline message needed — the main page handles re-auth)
- `fetchWithAuth` for POST requests in admin.js omits pre-existing `authHeaders()` spread from `options.headers` since `fetchWithAuth` sets them itself; this avoids double-header application
- `loadFiles()` retry uses a separate data path rather than re-reading the response body, avoiding stream-already-consumed errors

## Deviations from Plan

None — plan executed exactly as written. One minor implementation detail: the `loadFiles()` retry section was restructured slightly (separate `if/else` branches instead of fall-through) to avoid reading an already-consumed response body. This is a correctness fix within the spirit of the plan.

## Issues Encountered
- The original plan's `loadFiles()` retry snippet had a subtle double-consume bug (sets `response = retry` then calls `readJsonSafe(response)` after `retryData` already consumed the body). Fixed by using separate data branches rather than re-assigning `response`.

## Next Phase Readiness
- Auth flow fixed; ready for human verification (checkpoint 3 in this plan)
- No behavioral regression expected in login, logout, signup, or file operations

---
*Quick task: 260323-rdr*
*Completed: 2026-03-23*

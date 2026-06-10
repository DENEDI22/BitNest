# Phase 6 Research: Identity and Session Foundation

**Date:** 2026-03-19
**Scope:** Plan-ready technical guidance for `AUTH-01..AUTH-04`

## Research Outcome

Phase 6 should implement username/password authentication with short-lived access JWT and rotating refresh tokens, then wire the existing static frontend to an auth-first flow. Keep backend architecture aligned with current thin-controller + service pattern and EF Core migrations.

## Constraints and Inputs

- Locked decisions from `06-CONTEXT.md`:
  - `/auth/*` endpoints required: `/auth/signup`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/auth/me`
  - Access lifetime around 15 minutes with refresh flow
  - Remember-me option required
  - Logout revokes refresh token server-side
  - Stable auth error JSON shape: `{ code, message }`
  - Username normalized to lowercase handle format `[a-z0-9._-]+`
  - Password minimum length 8
- Existing stack constraints:
  - ASP.NET Core + EF Core + PostgreSQL
  - Vanilla JS frontend (`FrontEnd/main.js`) with direct DOM updates

## Recommended Technical Approach

### 1) Data model additions

- Add `User` entity with normalized username, password hash, created/updated timestamps, active flag.
- Add `RefreshSession` (or `RefreshToken`) entity to support token rotation and revocation:
  - `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt`, `ReplacedById`, `RememberMe`, metadata (`CreatedAt`, `CreatedByIp` optional).
- Configure indexes:
  - unique `User.NormalizedUsername`
  - index on `RefreshSession.UserId`, `RefreshSession.ExpiresAt`

### 2) Auth services and token strategy

- Use JWT bearer auth in ASP.NET Core middleware.
- Access token:
  - signed JWT, 15 minute expiry
  - claims: user id, username, auth version/session identifier
- Refresh token:
  - random opaque secret generated server-side
  - persist only hashed form in database
  - rotate on every `/auth/refresh`; revoke previous token in same transaction
- Logout:
  - revoke current refresh token record
  - clear refresh cookie and invalidate frontend session state

### 3) Endpoint contracts

- `POST /auth/signup`: validates username + password policy, rejects duplicates with stable error.
- `POST /auth/login`: validates credentials, issues access JWT + refresh cookie/token.
- `POST /auth/refresh`: validates active refresh token, rotates token, returns fresh access JWT.
- `POST /auth/logout`: revokes refresh token and clears cookie.
- `GET /auth/me`: protected endpoint returning current identity payload.

### 4) Frontend auth-first flow

- Add auth state bootstrap on app start:
  - block file UI until `/auth/me` or refresh attempt resolves
  - if refresh fails, show sign-in screen with short message
- Add auth UI in root shell for signup/login with remember-me checkbox.
- After successful login/signup, load existing file screen.
- On logout success, show brief confirmation and return to sign-in screen.
- For multi-tab behavior, rely on protected action failure/401 handling to enforce logout on next action.

## Standard Stack and Patterns

- Password hashing: PBKDF2/Argon2/bcrypt via .NET library already acceptable in ecosystem; choose one deterministic implementation and keep parameters explicit.
- API style: controller delegates to service; service returns typed result/error codes.
- Persistence: EF Core entities + migration in `BitNest/Migrations`.
- Error response contract: central helper or shared DTO for `{ code, message }`.

## Common Pitfalls to Avoid

- Storing raw refresh tokens (must store hashes).
- Non-rotating refresh tokens (violates AUTH-04 intent).
- Missing normalized username uniqueness check (race/duplicate risk).
- Returning inconsistent auth error body shape.
- Frontend rendering file UI before auth resolution.

## Plan Implications

- Split phase into three execution plans matching roadmap:
  1. Data model + hashing + migrations.
  2. Auth endpoints + middleware + token lifecycle.
  3. Frontend auth entry points + session handling.
- Ensure requirement coverage mapping:
  - AUTH-01, AUTH-02 in plans 01+03
  - AUTH-03 in plans 02+03
  - AUTH-04 in plan 02 (+frontend handling in 03)

## Validation Architecture

- Test infrastructure likely present via backend test project; if absent, create minimal auth-focused test scaffold in Wave 0.
- Quick validation commands should target focused auth tests and build:
  - `dotnet test --filter "FullyQualifiedName~Auth"`
  - `dotnet test`
- Frontend verification should include script-level checks for auth bootstrap and logout UX plus API interaction paths.

## RESEARCH COMPLETE

Phase 6 has sufficient implementation guidance for planning.

# Roadmap: BitNest

## Overview

This roadmap continues from historical phases 1-5 and defines milestone `v0.0.3-alpha Auth + Sharepoint` as phases 6-9. It introduces authentication and user/admin management first, then enforces file access boundaries, then adds temporary sharepoint links, and finally adds scoped public dropbox upload.

## Phases

- [ ] **Phase 6: Identity and Session Foundation** - Add auth model, JWT lifecycle, and frontend auth entry points
- [ ] **Phase 7: User Management and File Access Enforcement** - Add admin user controls and enforce owner/grant access across file flows
- [ ] **Phase 8: Sharepoint Expiring Download Links** - Add temporary scoped link generation/management and public download access
- [ ] **Phase 9: Sharepoint Dropbox Upload** - Add public upload flow scoped by active sharepoint links

## Phase Details

### Phase 6: Identity and Session Foundation
**Goal**: Establish secure user identity, authentication APIs, and web frontend auth flows.
**Depends on**: Phase 5
**Requirements**: [AUTH-01, AUTH-02, AUTH-03, AUTH-04]
**Success Criteria** (what must be TRUE):
  1. User can sign up and sign in from the web frontend using username and password.
  2. Authenticated API requests succeed with valid access token and fail with invalid/expired token.
  3. Refresh flow rotates refresh tokens and issues new access tokens.
  4. User can sign out and prior session credentials are no longer accepted.
**Plans**: 3 plans

Plans:
- [ ] 06-01-PLAN.md — Add auth entities, password hashing primitives, and migration baseline
- [ ] 06-02-PLAN.md — Implement `/auth/*` APIs, refresh rotation, and JWT middleware wiring
- [ ] 06-03-PLAN.md — Build auth-first frontend flow with signup/login/logout and startup auth gate

### Phase 7: User Management and File Access Enforcement
**Goal**: Provide admin user controls and enforce owner/grant-based access in existing storage flows.
**Depends on**: Phase 6
**Requirements**: [USER-01, USER-02, USER-03, ACCS-01, ACCS-02, ACCS-03, ACCS-04, ACCS-05]
**Success Criteria** (what must be TRUE):
  1. Admin can list users, disable users, and create new user accounts from web frontend controls.
  2. File metadata list returns only files owned by current user or explicitly granted.
  3. Download and delete operations reject unauthorized users and allow authorized users.
  4. Frontend file list/actions only render authorized resources/actions for current user.
**Plans**: 3 plans

Plans:
- [ ] 07-01: Add role/admin and file-grant persistence model with migrations
- [ ] 07-02: Enforce backend authorization across list/download/delete endpoints
- [ ] 07-03: Add frontend admin user-management and access-aware file UI behavior

### Phase 8: Sharepoint Expiring Download Links
**Goal**: Add secure temporary sharepoint links for selected files with public download access.
**Depends on**: Phase 7
**Requirements**: [SHRP-01, SHRP-02, SHRP-04, SHRP-05]
**Success Criteria** (what must be TRUE):
  1. Authenticated user can create sharepoint links for selected files with explicit expiration.
  2. Authenticated user can view active sharepoint links in web frontend management area.
  3. Public download works only for files in active non-expired link scope.
  4. Expired links are rejected for all sharepoint access attempts.
**Plans**: 2 plans

Plans:
- [ ] 08-01: Implement sharepoint link model, token generation, expiry validation, and download API
- [ ] 08-02: Add frontend sharepoint creation and active-link management UI

### Phase 9: Sharepoint Dropbox Upload
**Goal**: Add scoped third-party upload flow using valid sharepoint links.
**Depends on**: Phase 8
**Requirements**: [SHRP-03]
**Success Criteria** (what must be TRUE):
  1. Public user can upload file(s) only through valid non-expired sharepoint link scope.
  2. Upload attempts outside scope or after expiry are rejected.
  3. Uploaded files are attributed and stored in expected owner/sharepoint context.
**Plans**: 2 plans

Plans:
- [ ] 09-01: Implement backend dropbox upload endpoint with strict scope and expiry checks
- [ ] 09-02: Add public upload UX entry point for sharepoint link consumers

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 6. Identity and Session Foundation | 0/3 | Not started | - |
| 7. User Management and File Access Enforcement | 0/3 | Not started | - |
| 8. Sharepoint Expiring Download Links | 0/2 | Not started | - |
| 9. Sharepoint Dropbox Upload | 0/2 | Not started | - |

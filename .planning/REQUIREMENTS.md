# Requirements: BitNest

**Defined:** 2026-03-19
**Milestone:** v0.0.3-alpha Auth + Sharepoint
**Core Value:** Users can reliably store and retrieve files on their own infrastructure with a simple web workflow.

## v1 Requirements

Requirements for milestone `v0.0.3-alpha`. Each maps to roadmap phases.

### Authentication

- [ ] **AUTH-01**: User can sign up with username and password via web frontend and API
- [ ] **AUTH-02**: User can sign in with username and password via web frontend and API
- [ ] **AUTH-03**: User can sign out from the web frontend and invalidate active session
- [ ] **AUTH-04**: User can obtain renewed access via rotating refresh token flow

### User Management

- [ ] **USER-01**: Admin can open a web frontend user-management area and list all users with account status
- [ ] **USER-02**: Admin can disable a user account from the web frontend to block further access
- [ ] **USER-03**: Admin can create a user account with initial username and password for handoff to a physical user

### Access Control

- [ ] **ACCS-01**: Authenticated user can view metadata only for owned files or files with explicit grant access
- [ ] **ACCS-02**: Download endpoint enforces owner/grant authorization before returning file content
- [ ] **ACCS-03**: Delete endpoint enforces owner/grant authorization before file deletion action
- [ ] **ACCS-04**: Web frontend file list and actions only surface files/actions the current user is authorized to access
- [ ] **ACCS-05**: System stores grant-based access model for file permissions

### Sharepoint Links

- [ ] **SHRP-01**: Authenticated user can generate a temporary sharepoint link for selected files with user-defined expiration from the web frontend
- [ ] **SHRP-02**: Unauthenticated user can download only files in valid non-expired sharepoint link scope
- [ ] **SHRP-03**: Unauthenticated user can upload file(s) through valid sharepoint link into scoped dropbox flow
- [ ] **SHRP-04**: System rejects expired sharepoint links for both download and upload operations
- [ ] **SHRP-05**: Web frontend provides sharepoint management entry points (create and view active links) for authenticated users

## v2 Requirements

Deferred to future release.

### Collaboration

- **COLL-01**: Authenticated users can directly share files with other registered users
- **COLL-02**: Authenticated users can manage incoming/outgoing cross-user share permissions

## Out of Scope

| Feature | Reason |
|---------|--------|
| Cross-user file sharing UX | Explicitly deferred by milestone scope to avoid permission-model expansion |
| OAuth/social login | Not required for this milestone; username/password is sufficient |
| Mobile-native clients | Current milestone targets existing web frontend only |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTH-01 | Phase 6 | Pending |
| AUTH-02 | Phase 6 | Pending |
| AUTH-03 | Phase 6 | Pending |
| AUTH-04 | Phase 6 | Pending |
| USER-01 | Phase 7 | Pending |
| USER-02 | Phase 7 | Pending |
| USER-03 | Phase 7 | Pending |
| ACCS-01 | Phase 7 | Pending |
| ACCS-02 | Phase 7 | Pending |
| ACCS-03 | Phase 7 | Pending |
| ACCS-04 | Phase 7 | Pending |
| ACCS-05 | Phase 7 | Pending |
| SHRP-01 | Phase 8 | Pending |
| SHRP-02 | Phase 8 | Pending |
| SHRP-03 | Phase 9 | Pending |
| SHRP-04 | Phase 8 | Pending |
| SHRP-05 | Phase 8 | Pending |

**Coverage:**
- v1 requirements: 17 total
- Mapped to phases: 17
- Unmapped: 0

---
*Requirements defined: 2026-03-19*
*Last updated: 2026-03-19 after roadmap creation*

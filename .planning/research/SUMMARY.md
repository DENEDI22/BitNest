# Project Research Summary

**Project:** BitNest
**Domain:** Self-hosted file storage with auth, access control, and temporary public links
**Researched:** 2026-03-19
**Confidence:** HIGH

## Executive Summary

For this milestone, the strongest path is to keep BitNest's existing ASP.NET Core + EF Core + PostgreSQL architecture and add identity/access capabilities as first-class backend concerns. The requested JWT access+refresh model fits well for API-oriented expansion while preserving current frontend compatibility.

The major implementation risk is inconsistent authorization coverage across endpoints. That risk is amplified by adding public sharepoint links with upload support. The roadmap should therefore enforce auth + access policy primitives first, then layer temporary link behavior, then add public dropbox upload with strict scope checks.

## Key Findings

### Recommended Stack

Use `Microsoft.AspNetCore.Authentication.JwtBearer` + JWT token lifecycle, with persistence in PostgreSQL through existing EF Core patterns. Keep file/chunk storage architecture intact and add access metadata tables instead of introducing external storage or queue systems.

**Core technologies:**
- ASP.NET Core 9.x: API and auth middleware integration
- EF Core + Npgsql 9.x: users, grants, tokens, and link records
- PostgreSQL 16.x: durable relational access model and expiry queries

### Expected Features

**Must have (table stakes):**
- JWT auth flows (signup/login/logout/refresh)
- Ownership/grant-based metadata and file access filtering
- Expiring sharepoint links scoped to selected files

**Should have (competitive):**
- Sharepoint links supporting both download and dropbox-style upload
- User-defined link expiry windows

**Defer (v2+):**
- Full cross-user sharing and collaboration graph

### Architecture Approach

Add dedicated `AuthService`, `AccessService`, and `LinkService`; keep `StorageService` focused on storage concerns. Introduce sharepoint-specific endpoints so public-link semantics do not weaken authenticated APIs.

### Critical Pitfalls

1. **Incomplete authorization coverage** - centralize and enforce access checks in every file path.
2. **Weak sharepoint token model** - require high-entropy tokens and strict expiry validation.
3. **Public upload escalation** - isolate dropbox upload flow with hard scope and limits.

## Implications for Roadmap

### Phase 6: Identity and Session Foundation
**Rationale:** All access and link features depend on user identity and token lifecycle.
**Delivers:** User model, password hashing, JWT access/refresh, auth endpoints.

### Phase 7: File Access Control Enforcement
**Rationale:** Secure existing endpoints before exposing temporary public links.
**Delivers:** Ownership and grant model, endpoint-level authorization, scoped metadata queries.

### Phase 8: Sharepoint Link Core (Expiring Download)
**Rationale:** Introduce temporary link model with smallest safe public surface first.
**Delivers:** Link creation/revocation, expiry enforcement, selected-file download scope.

### Phase 9: Sharepoint Dropbox Upload
**Rationale:** Highest abuse risk; implement after core link security is proven.
**Delivers:** Public upload via link scope with size/type constraints and audit attribution.

### Phase Ordering Rationale

- Identity -> authorization -> public links -> public upload follows strict dependency chain.
- This order reduces the chance of unauthorized data exposure during incremental rollout.
- Security pitfalls are addressed in the same phase they can first appear.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Builds directly on current project stack and official auth patterns |
| Features | HIGH | User goals are explicit and consistent |
| Architecture | HIGH | Integration points in existing codebase are clear |
| Pitfalls | HIGH | Risks are common and well-understood for this domain |

**Overall confidence:** HIGH

---
*Research completed: 2026-03-19*
*Ready for roadmap: yes*

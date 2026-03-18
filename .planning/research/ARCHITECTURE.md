# Architecture Research

**Domain:** Existing ASP.NET Core storage API extended with auth, ACL, and temporary public links
**Researched:** 2026-03-19
**Confidence:** HIGH

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    API / Controller Layer                  │
├─────────────────────────────────────────────────────────────┤
│  AuthController  StorageController  SharepointController   │
├─────────────────────────────────────────────────────────────┤
│                Application / Service Layer                 │
├─────────────────────────────────────────────────────────────┤
│ AuthService  AccessService  StorageService  LinkService    │
├─────────────────────────────────────────────────────────────┤
│                    Persistence Layer                       │
│  Users  RefreshTokens  FileMetadata  FileAccessGrants      │
│  SharepointLinks  SharepointLinkFiles  SharepointUploads   │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| AuthService | Credential validation, token issuance, token rotation | JWT + hashed passwords + refresh token store |
| AccessService | Owner/grant authorization decisions | Centralized policy checks before file operations |
| LinkService | Sharepoint link create/validate/expire logic | DB-backed token, expiry checks, scope mapping |

## Recommended Project Structure

```
BitNest/
├── Controllers/            # HTTP endpoints
│   ├── AuthController.cs
│   ├── StorageController.cs
│   └── SharepointController.cs
├── Services/               # Business logic
│   ├── AuthService.cs
│   ├── AccessService.cs
│   ├── StorageService.cs
│   └── LinkService.cs
├── Models/                 # EF entities
│   ├── User.cs
│   ├── RefreshToken.cs
│   ├── FileAccessGrant.cs
│   └── Sharepoint*.cs
└── Data/AppDbContext.cs    # DbSet + model relations
```

### Structure Rationale

- **Services split by responsibility:** avoids overloading `StorageService` with auth/link concerns.
- **Dedicated sharepoint controller/service:** keeps public-link behavior isolated from authenticated APIs.

## Architectural Patterns

### Pattern 1: Policy-First Authorization

**What:** Authorize by owner/grant checks in one reusable place.
**When to use:** Every read/write endpoint touching file metadata or file content.
**Trade-offs:** Slightly more boilerplate; much lower risk of missing checks.

### Pattern 2: DB-Backed Expiring Links

**What:** Store token hash, expiry, scope, permissions, and creator.
**When to use:** Temporary links with upload/download behavior and revocation support.
**Trade-offs:** Extra tables and checks; enables auditability and strict controls.

### Pattern 3: Refresh Token Rotation

**What:** Rotate refresh token on use and invalidate old token.
**When to use:** JWT session continuity for web app and future clients.
**Trade-offs:** More state management; significantly better compromise containment.

## Data Flow

### Key Data Flows

1. **Authenticated file list:** JWT -> user identity -> access filter -> metadata response.
2. **Sharepoint download/upload:** public token -> link validation (expiry/scope/permission) -> allowed transfer only.

## Integration Points

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `StorageController` ↔ `AccessService` | Direct service call | Mandatory check before metadata/download/delete |
| `SharepointController` ↔ `LinkService` | Direct service call | Validate token and permissions on every request |
| `LinkService` ↔ `StorageService` | Service abstraction | Reuse chunk/file storage path with scoped guardrails |

### Build Order Suggestion

1. User/auth model + JWT plumbing.
2. Ownership/grant schema + access filtering in storage endpoints.
3. Sharepoint link schema + validation + public download.
4. Sharepoint dropbox upload flow + auditing.

## Sources

- Existing architecture docs in `.planning/codebase/ARCHITECTURE.md` and `.planning/codebase/STRUCTURE.md`
- ASP.NET Core auth and authorization patterns
- Secure temporary-link architecture patterns from storage systems

---
*Architecture research for: BitNest auth and controlled sharing*
*Researched: 2026-03-19*

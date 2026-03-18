# Stack Research

**Domain:** Self-hosted file storage with authentication, authorization, and temporary public share links
**Researched:** 2026-03-19
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| ASP.NET Core | 9.x | API, auth endpoints, policy-based authorization | Already in use in `BitNest/`; first-class authn/z support and minimal migration cost |
| Entity Framework Core + Npgsql | 9.x | Persist users, sessions/tokens, grants, share links | Existing persistence layer in `BitNest/Data/AppDbContext.cs`; keeps data model unified |
| PostgreSQL | 16.x | Durable relational storage for identity and access metadata | Existing runtime in `compose.yaml`; ideal for ACL and expiring-link queries |
| JWT (Bearer tokens) | RFC 7519 + .NET JWT libs | Access + refresh token auth model | Matches requested API-first auth flow and future clients |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.x | JWT validation middleware | Required for secure protected endpoints |
| `System.IdentityModel.Tokens.Jwt` | 8.x+ | Token creation and claims handling | Required when minting access/refresh tokens |
| `Microsoft.AspNetCore.Identity` (optional) | 9.x | Password hashing/user lifecycle primitives | Use if you want robust identity primitives with less custom auth code |
| `BCrypt.Net-Next` (alternative) | latest stable | Password hashing without full Identity stack | Use only if implementing a custom lightweight auth layer |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| EF Core Migrations | Schema evolution for users/ACL/share links | Continue current migration pattern in `BitNest/Migrations/` |
| Postman/Bruno collection | Verify auth/link lifecycles | Useful for testing token expiry and access boundaries |
| Structured logging (Serilog) | Audit auth and public-link access events | Add event IDs and actor identifiers |

## Installation

```bash
# Auth packages
dotnet add BitNest/BitNest.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add BitNest/BitNest.csproj package System.IdentityModel.Tokens.Jwt

# Optional identity primitives
dotnet add BitNest/BitNest.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| JWT access + refresh | Cookie sessions | Browser-only apps with no third-party API clients |
| ASP.NET Core Identity primitives | Fully custom user auth tables | Only if strict minimal schema and full control are required |
| DB-backed share links | Stateless signed URL only | When file metadata and grants are fully object-store-native |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Plaintext or reversible password storage | Critical security risk | Strong one-way password hashing |
| Long-lived non-rotating refresh tokens | High account takeover blast radius | Rotating refresh tokens with revocation |
| File-path-based authorization only | Easy bypass and inconsistent policy | DB-backed ownership/grant checks per file ID |

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| `Microsoft.AspNetCore.Authentication.JwtBearer@9.x` | `net9.0` | Align with existing target framework in `BitNest/BitNest.csproj` |
| `Npgsql.EntityFrameworkCore.PostgreSQL@9.x` | EF Core 9.x | Already aligned in current project |

## Sources

- Microsoft ASP.NET Core auth docs - JWT bearer and authorization policy patterns
- EF Core docs - relationship modeling and migrations
- OWASP ASVS / Cheat Sheets - auth, token, and file-access security guidance

---
*Stack research for: BitNest auth and controlled sharing*
*Researched: 2026-03-19*

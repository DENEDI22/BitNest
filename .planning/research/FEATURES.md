# Feature Research

**Domain:** Authenticated personal file storage with temporary public share links
**Researched:** 2026-03-19
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Signup/login/logout | Baseline for private storage products | MEDIUM | Needed before any per-user metadata filtering |
| Password hashing + secure auth flows | Security expectation for account systems | MEDIUM | Never store raw passwords |
| Ownership-based file visibility | Core trust model: users see only their own data | HIGH | Must apply to list, download, delete, and metadata endpoints |
| Token expiration and refresh | Expected for JWT UX and security | MEDIUM | Short-lived access + longer refresh |
| Temporary expiring share links | Requested collaboration-lite capability | HIGH | Time-bounded public access with strict scope |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Sharepoint link supports download + upload dropbox | One temporary link supports two-way exchange | HIGH | Must isolate upload path to link scope |
| Fine-grained link scope (selected files) | Better control than broad folder sharing | MEDIUM | Link-to-file mapping in DB |
| User-defined expiry windows | Flexible security and usability | LOW | Validate max/min bounds server-side |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Full cross-user sharing UI now | Feels collaborative and powerful | Scope explosion (permissions graph, invites, UX) | Defer; keep public temporary links only |
| Unlimited link lifetime | Convenience | Turns temporary links into permanent exposure | Enforce required expiry and reasonable max TTL |

## Feature Dependencies

```
[Sharepoint link access]
    └──requires──> [Authentication + User model]
                        └──requires──> [JWT + refresh lifecycle]

[Per-user metadata filtering]
    └──requires──> [Ownership/grant schema]

[Public upload dropbox]
    └──requires──> [Scoped link permissions + audit fields]
```

### Dependency Notes

- **Sharepoint links require user identities:** link creator and ownership must be attributable.
- **Metadata filtering requires ACL data:** existing file model must include owner/grant information.
- **Public upload requires scoped permission model:** do not reuse full authenticated upload path without scope checks.

## MVP Definition

### Launch With (v1)

- [ ] JWT auth with signup/login/logout and refresh flow
- [ ] User ownership controls for file metadata/list/download/delete
- [ ] Expiring sharepoint links for selected files
- [ ] Link-scoped public download and dropbox-style upload

### Add After Validation (v1.x)

- [ ] Admin user controls UI polish and audit dashboards
- [ ] Optional link revoke and usage counters

### Future Consideration (v2+)

- [ ] Cross-user direct file sharing with account-to-account permissions
- [ ] Multi-tenant organizations/teams and role hierarchies

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| JWT auth baseline | HIGH | MEDIUM | P1 |
| Ownership/ACL enforcement | HIGH | HIGH | P1 |
| Expiring sharepoint links | HIGH | HIGH | P1 |
| Admin controls | MEDIUM | MEDIUM | P2 |
| Link analytics and passcodes | MEDIUM | MEDIUM | P3 |

## Sources

- Existing BitNest product direction from user milestone goals
- Established patterns from self-hosted file tools (owner-scoped access + temporary links)
- Web security best practices for auth and public token links

---
*Feature research for: BitNest auth and temporary sharepoint links*
*Researched: 2026-03-19*

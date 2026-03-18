# Pitfalls Research

**Domain:** Auth + access control + temporary public links for file storage APIs
**Researched:** 2026-03-19
**Confidence:** HIGH

## Critical Pitfalls

### Pitfall 1: Incomplete Authorization Coverage

**What goes wrong:**
One endpoint enforces owner/grant checks, another leaks metadata or file bytes.

**Why it happens:**
Authorization logic is copied per controller and eventually diverges.

**How to avoid:**
Use a centralized access service/policy and require checks in every storage path.

**Warning signs:**
`GET /Storage/{page}` and `GET /Storage/download/{id}` return data without consistent user scope filtering.

**Phase to address:**
Access-control phase (early, before sharepoint links).

---

### Pitfall 2: Weak Sharepoint Link Token Design

**What goes wrong:**
Predictable tokens or non-expiring links allow unauthorized reuse.

**Why it happens:**
Teams optimize for speed and skip entropy, expiry validation, or token hashing at rest.

**How to avoid:**
Generate high-entropy random tokens, store only hashed token server-side, enforce expiry every request.

**Warning signs:**
Links are simple IDs, long-lived by default, or accepted after expiration boundaries.

**Phase to address:**
Sharepoint-link core phase.

---

### Pitfall 3: Upload Escalation via Public Link

**What goes wrong:**
Public upload endpoint becomes a general unauthenticated upload bypass.

**Why it happens:**
Dropbox upload flow reuses authenticated upload path without strict link-scope checks.

**How to avoid:**
Use dedicated public-upload path bound to link scope, size/type limits, and ownership attribution.

**Warning signs:**
Any valid link can upload arbitrary files outside intended scope or unlimited payloads.

**Phase to address:**
Public dropbox upload phase.

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Raw password storage or weak hashing | Full account compromise | Use strong password hashing with adaptive work factor |
| Non-rotating refresh tokens | Long-term hijacked sessions | Rotate and revoke refresh tokens |
| Returning too much metadata in public link flows | Sensitive info leak | Minimal DTOs for public endpoints |

## "Looks Done But Isn't" Checklist

- [ ] **JWT auth:** refresh rotation and revocation truly implemented, not just token issue
- [ ] **Access control:** all file endpoints enforce owner/grant checks consistently
- [ ] **Sharepoint links:** expiration verified server-side on every request
- [ ] **Public upload:** link scope, limits, and attribution enforced

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Incomplete authorization coverage | Access-control phase | Endpoint-by-endpoint auth matrix review |
| Weak sharepoint token design | Sharepoint link core phase | Token entropy/expiry tests + negative tests |
| Public upload escalation | Public dropbox phase | Unauthorized upload attempts fail by design |

## Sources

- OWASP guidance for authentication and authorization
- ASP.NET Core security best practices
- Existing project concerns in `.planning/codebase/CONCERNS.md`

---
*Pitfalls research for: BitNest auth and temporary links*
*Researched: 2026-03-19*

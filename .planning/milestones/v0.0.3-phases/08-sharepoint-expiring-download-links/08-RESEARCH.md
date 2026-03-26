# Phase 8 Research: Sharepoint Expiring Download Links

**Phase:** 08-sharepoint-expiring-download-links  
**Researched:** 2026-03-19  
**Researcher:** Claude (orchestrator fallback - subagent spawn unavailable)

## Research Question

What do I need to know to PLAN Phase 8 (secure temporary download links) well?

## Domain Understanding

### What We're Building

Time-limited, publicly-accessible download links (no auth required) for specific files, with:
- Authenticated link creation with user-defined expiration
- Public download via tokenized URL (token is the sole credential)
- Link management UI (view active links, revoke early)
- Distinct error handling for expired/invalid links

### Security Model

**Threat surface expanded by this phase:**
- Public endpoints (no authentication required)
- Token-based access control (URL contains credential)
- Time-based authorization (expiry validation)
- Potential enumeration attacks (guessing tokens)

## Standard Stack

### Token Generation Patterns

**Existing pattern in codebase** (`JwtTokenService.cs`):
```csharp
public string GenerateRefreshSecret()
{
    return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
```

**Analysis:**
- `RandomNumberGenerator.GetBytes(64)` produces cryptographically secure random 64-byte value
- Base64 encoding produces ~88-character string
- Collision probability: ~2^512 space (practically zero for millions of links)
- **Recommendation:** Reuse this exact pattern for sharepoint tokens

### Token Storage Patterns

**Existing pattern** (`RefreshSession.TokenHash`):
```csharp
public string HashRefreshSecret(string refreshSecret)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshSecret));
    return Convert.ToBase64String(bytes);
}
```

**Security principle:** Never store raw tokens in database
- Store: `SHA256(token)` 
- Client receives: raw token in URL
- Lookup: hash incoming token, query by TokenHash column
- Revocation: irreversible (can't reconstruct URL from hash)

**Recommendation:** Apply identical pattern to sharepoint links

### Expiry Validation Patterns

**Existing pattern** (`RefreshSession.IsActive`):
```csharp
public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
```

**Three-state model:**
1. **Active:** `RevokedAt is null && ExpiresAt > UtcNow`
2. **Expired:** `RevokedAt is null && ExpiresAt <= UtcNow`
3. **Revoked:** `RevokedAt is not null`

**Query optimization:** Index on `(TokenHash, ExpiresAt, RevokedAt)` for fast active-link lookups

**Recommendation:** 
- Model: `ExpiresAt` (DateTime), `RevokedAt` (DateTime?)
- Validation: Check both expiry and revocation in single query
- Cleanup: Periodic job to delete expired links (not in Phase 8 scope)

## Architecture Patterns

### Model Design

Based on `RefreshSession` and `FileGrant` patterns:

```csharp
public class SharepointLink
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public int CreatedByUserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public FileMetadata File { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    
    // Computed property
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
```

**Indexing strategy:**
- Unique index on `TokenHash` (primary lookup)
- Index on `(CreatedByUserId, RevokedAt, ExpiresAt)` for active-links view
- Foreign keys: `FileId` → `FileMetadata.Id`, `CreatedByUserId` → `User.Id`

**Foreign key delete behavior:**
- File deleted → cascade delete sharepoint links (file no longer exists)
- User deleted → restrict or set null (depends on business requirement - likely restrict)

### Controller Design

**Two controllers needed:**

1. **SharepointController** (authenticated - `/api/sharepoint/*`):
   - `POST /api/sharepoint/links` — Create link, returns raw token
   - `GET /api/sharepoint/links` — List active links for current user
   - `DELETE /api/sharepoint/links/{id}` — Revoke link by ID

2. **PublicShareController** (unauthenticated - `/api/share/{token}`):
   - `GET /api/share/{token}` — Get file metadata for valid token
   - `GET /api/share/{token}/download` — Stream file bytes for valid token

**Why separate controllers:**
- Clear security boundary (SharepointController requires auth, PublicShareController does not)
- Different authorization logic (user ownership vs token validity)
- Easier to audit public endpoint access

### Service Layer

**SharepointLinkService** responsibilities:
- `CreateLink(fileId, userId, expiresAt)` → returns raw token + link entity
- `GetActiveLinksForUser(userId)` → returns list of user's active links
- `RevokeLink(linkId, userId)` → sets RevokedAt (enforces ownership)
- `ValidateTokenAndGetFile(token)` → hashes token, checks expiry/revocation, returns file
- `GetFileByToken(token)` → returns file metadata if token valid

**Token generation flow:**
```csharp
// 1. Generate raw token (never persisted)
var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

// 2. Hash for storage
var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

// 3. Create entity
var link = new SharepointLink { TokenHash = tokenHash, ... };
context.Add(link);
await context.SaveChangesAsync();

// 4. Return raw token to caller (this is the ONLY time it exists in plaintext)
return rawToken;
```

### URL Structure

**Public download URL pattern:**
- Format: `https://bitnest.example.com/share/{token}`
- Token in path (not query string) for cleaner sharing
- Frontend router handles `/share/*` route
- Fetch metadata from `/api/share/{token}`, show download page
- Download triggered by button → `/api/share/{token}/download`

**Why path-based:**
- Cleaner for users sharing links
- Query strings sometimes stripped by proxies/firewalls
- Consistent with modern link-sharing UX (Dropbox, Google Drive use path-based)

### Frontend Architecture

**New routes:**
1. `#links` — Authenticated user's active links management
2. `/share/{token}` — Public download landing page (not hash-routed, separate path)
3. `/share/expired` — Error page for invalid/expired tokens

**Hash routing extension** (`main.js` patterns):
```javascript
// Add to existing hashchange handler
function routeToHash() {
  const hash = window.location.hash.slice(1);
  if (hash === 'links') {
    loadActiveLinksView();
  }
  // ... existing routes
}
```

**Public download page** (separate from SPA):
- Minimal HTML (no auth required)
- Fetch `/api/share/{token}` → show file name, size, expiry
- Download button → `/api/share/{token}/download`
- Error handling → `/share/expired` page

**Header nav addition:**
```html
<button id="linksNavButton">Links</button>
```

Shown for all authenticated users (not admin-only like `#admin`).

## Validation Architecture

### Test Strategy

**Unit tests** (Services):
- Token generation uniqueness (generate 1000, verify no collisions)
- Hash consistency (same token → same hash)
- Expiry validation logic (active, expired, revoked states)

**Integration tests** (Controllers):
- Create link → returns valid URL with token
- Download with valid token → returns file bytes
- Download with expired token → returns 404/410
- Download with revoked token → returns 404/410
- Download with invalid token → returns 404
- Revoke link → subsequent download fails

**E2E scenarios:**
1. Authenticated user creates link → receives URL
2. Public user visits URL → sees download page
3. Public user clicks download → file downloads
4. Time passes, link expires → download fails with branded error
5. User revokes link → download fails immediately

### Security Testing

**Attack vectors to validate:**
1. **Token enumeration:** Random guessing should fail (64-byte token = 2^512 space)
2. **Timing attacks:** Token validation timing should not leak existence (constant-time comparison)
3. **Expired token replay:** Expired tokens should be rejected even if token format is valid
4. **Revoked token replay:** Revoked tokens should be rejected
5. **Authorization bypass:** Cannot revoke another user's link
6. **File access bypass:** Cannot download files not in active link scope

**Implementation notes:**
- Use constant-time hash comparison (`CryptographicOperations.FixedTimeEquals`)
- Return same error response for expired/revoked/invalid (don't leak state)
- Rate-limit public endpoints to prevent brute-force enumeration

## Common Pitfalls

### Don't Hand-Roll Crypto

**Bad:**
```csharp
var token = Guid.NewGuid().ToString(); // Only 2^128 space, predictable
```

**Good:**
```csharp
var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)); // 2^512 space
```

**Rationale:** `Guid` is designed for uniqueness, not security. `RandomNumberGenerator` is CSPRNG.

### Don't Store Raw Tokens

**Bad:**
```csharp
new SharepointLink { Token = rawToken } // Compromised DB = all links leaked
```

**Good:**
```csharp
new SharepointLink { TokenHash = SHA256(rawToken) } // DB compromise doesn't reveal tokens
```

**Rationale:** Database backups, logs, error dumps could expose raw tokens.

### Don't Trust Client-Side Expiry

**Bad:**
```javascript
if (new Date(link.expiresAt) < new Date()) return; // Client can bypass
```

**Good:**
```csharp
if (link.ExpiresAt <= DateTime.UtcNow) return Unauthorized(); // Server enforces
```

**Rationale:** Client validation is UX, server validation is security.

### Don't Leak Token Validity in Errors

**Bad:**
```
404: Token not found
410: Token expired
403: Token revoked
```

**Good:**
```
404: This link is no longer valid
```

**Rationale:** Revealing why a token failed helps attackers enumerate valid-but-expired tokens.

### Don't Skip Constant-Time Comparison

**Bad:**
```csharp
if (storedHash == incomingHash) // Early return leaks timing
```

**Good:**
```csharp
if (CryptographicOperations.FixedTimeEquals(storedBytes, incomingBytes))
```

**Rationale:** Timing attacks can reveal partial matches byte-by-byte.

## Integration Points

### Database Schema

**Migration tasks:**
1. Add `SharepointLinks` table with columns above
2. Add unique index on `TokenHash`
3. Add index on `(CreatedByUserId, RevokedAt, ExpiresAt)`
4. Add foreign keys with cascade/restrict behavior

### API Endpoints

**New authenticated endpoints:**
- `POST /api/sharepoint/links { fileId, expiresAt }` → `{ id, token, url, expiresAt }`
- `GET /api/sharepoint/links` → `{ links: [...] }`
- `DELETE /api/sharepoint/links/{id}` → `204 No Content`

**New public endpoints:**
- `GET /api/share/{token}` → `{ fileName, fileSize, expiresAt }`
- `GET /api/share/{token}/download` → file stream

### Frontend Views

**New authenticated view (`#links`):**
- Table: File Name | Created | Expires | Actions (Copy URL, Revoke)
- Empty state: "No active links — share a file from the Files view"

**Per-file action (Files view):**
- Add "Share" button to each file row
- Inline modal/popup with expiry presets + custom date
- Show generated URL with copy button

**New public pages:**
- `/share/{token}` → Download landing page
- `/share/expired` → Expired link error page

## What NOT to Do

### Avoid UUIDs/GUIDs for Public Tokens

- GUIDs are 128-bit (vs 512-bit random tokens)
- Sequential GUIDs are predictable
- Use `RandomNumberGenerator.GetBytes(64)` instead

### Avoid Query-String Tokens

- `?token=...` gets logged by proxies, analytics
- Path-based (`/share/{token}`) is safer

### Avoid Per-Request Expiry Checks Without Caching

- Hitting DB for every download validation is expensive
- Cache active token hashes in memory with short TTL (if needed for high traffic)
- For Phase 8 scope: direct DB query is fine (optimize later if needed)

### Avoid Exposing Link IDs in Public URLs

- `/share/123` leaks sequential IDs (enumeration risk)
- `/share/{token}` uses opaque, unguessable token

### Avoid Email-Based Link Sharing (in Phase 8)

- Phase 8 is link generation and management only
- Email integration is out of scope (defer to future phase)

## Decisions for Planner

### Must Include in Plans

1. **Model + Migration:**
   - `SharepointLink` entity with `TokenHash`, `ExpiresAt`, `RevokedAt`
   - Indexes: unique on `TokenHash`, composite on `(CreatedByUserId, RevokedAt, ExpiresAt)`

2. **Service Layer:**
   - `SharepointLinkService` with create/validate/revoke methods
   - Token generation using `RandomNumberGenerator.GetBytes(64)`
   - Hash storage using SHA256

3. **Authenticated Endpoints:**
   - `SharepointController` with POST/GET/DELETE for link management

4. **Public Endpoints:**
   - `PublicShareController` with GET metadata + download stream

5. **Frontend - Authenticated:**
   - `#links` route for active links view
   - "Share" button in file list rows
   - Expiry preset UI (1h, 24h, 7d, 30d, custom)

6. **Frontend - Public:**
   - `/share/{token}` download landing page
   - `/share/expired` error page
   - Copy-to-clipboard for generated URLs

7. **Validation Tests:**
   - Integration tests for all endpoints
   - Security tests for expired/revoked/invalid tokens
   - E2E test for full create → share → download → revoke flow

### Implementation Sequence

**Plan 1: Backend (Model + API + Service)**
- Task 1: Create `SharepointLink` model and migration
- Task 2: Implement `SharepointLinkService` with token generation/validation
- Task 3: Create `SharepointController` (authenticated endpoints)
- Task 4: Create `PublicShareController` (public download endpoints)
- Task 5: Add integration tests for all endpoints

**Plan 2: Frontend (UI + Public Pages)**
- Task 1: Add `#links` route and active links management view
- Task 2: Add "Share" button to file list with expiry selection
- Task 3: Create `/share/{token}` public download landing page
- Task 4: Create `/share/expired` error page
- Task 5: Wire header nav "Links" button

**Dependency:** Plan 2 depends on Plan 1 (backend APIs must exist first)

### Estimated Complexity

- **Model + Service:** Medium (reuses existing patterns, straightforward)
- **Controllers:** Medium (two controllers, security-sensitive)
- **Frontend - Authenticated:** Low (extends existing hash routing)
- **Frontend - Public:** Medium (new non-SPA page, separate routing)
- **Testing:** High (security-critical, many edge cases)

**Context budget:** Two plans with 2-3 tasks each should fit ~50% context target

## Validation Architecture

### Nyquist Validation (Automated Verification)

**Goal:** Prove Phase 8 success criteria through automated tests before human verification.

**Test categories:**

1. **Link Creation Validation:**
   - Test: Authenticated user can create link with 1h expiry preset
   - Test: Authenticated user can create link with custom date/time
   - Test: Created link returns valid token and URL
   - Verify: Token is 88-character base64 string
   - Verify: URL format is `/share/{token}`

2. **Active Links Management Validation:**
   - Test: User can list their own active links
   - Test: Active links show file name, created date, expiry time
   - Test: User cannot see other users' links
   - Test: Revoked links do not appear in active list
   - Verify: Empty state shows guidance message

3. **Public Download Validation:**
   - Test: Valid token returns file metadata (name, size, expiry)
   - Test: Valid token download returns file bytes matching original
   - Test: Downloaded file has correct Content-Type and Content-Disposition headers
   - Verify: No authentication required for public download

4. **Expiry Enforcement Validation:**
   - Test: Expired token (ExpiresAt < UtcNow) returns 404/410
   - Test: Revoked token (RevokedAt != null) returns 404/410
   - Test: Invalid token (not in DB) returns 404
   - Test: Expired/revoked/invalid all return same error page
   - Verify: Error page shows "This link is no longer valid" message

5. **Security Validation:**
   - Test: Token enumeration (random guesses) fail consistently
   - Test: Timing attack resistance (expired vs invalid return in similar time)
   - Test: User A cannot revoke User B's link (authorization check)
   - Test: Public download fails for files not in active link scope
   - Verify: All security boundaries enforced server-side

**Automated test commands:**
```bash
# Backend integration tests
dotnet test --filter "Category=SharepointLinks"

# Frontend E2E tests (if framework available)
npm test -- --filter=sharepoint

# Security tests
dotnet test --filter "Category=Security"
```

**Human verification points:**
- Visual check: Download page branding and layout
- UX check: Copy-to-clipboard feels instant
- Error page: Expired link message is clear and helpful

---

*Research complete: 2026-03-19*  
*Ready for planning*

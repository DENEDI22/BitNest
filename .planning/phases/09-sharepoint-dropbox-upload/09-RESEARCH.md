# Phase 9: Sharepoint Dropbox Upload - Research

**Researched:** 2026-03-20
**Domain:** ASP.NET Core (net9.0) — EF Core migration, public multipart upload endpoint, vanilla JS file upload with XHR progress
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Link type — upload slots vs download links**
- Upload slots are a separate link type, not tied to any specific file. They are generic dropbox-style drop zones into the owner's storage space.
- The same `SharepointLink` entity is used, with a new `LinkType` enum column (`Download` | `Upload`). A migration adds the column with `Download` as the default for existing rows.
- Upload slots are created from the `#links` page via a "New upload slot" action — not from a per-file Share button.
- The `#links` page shows all active links (both types) in one unified list. Each row has a type badge indicating `Download` or `Upload`.
- Expiry presets are the same as download links (1 hour, 24 hours, 7 days, 30 days, custom date/time).

**Upload slot creation**
- Owner provides: expiry (required, same presets as download links) and optional description/label (e.g., "Photos from the event") shown to the uploader.
- Owner sets a file count limit at creation time (required). Presets: 1, 5, 10, 25 files — plus a custom number input.
- Description and file limit are stored on the `SharepointLink` entity (new nullable `Description` and nullable int `MaxFileCount` columns).

**Upload UX — public upload page**
- A separate `upload.html` page (distinct from `share.html`) served at a separate URL path (e.g., `/upload?token=...`).
- Page shows a full context card: owner username, slot creation date, expiry, optional description, remaining file count (if limit not yet hit), and a dropzone/file picker.
- Upload is one file at a time. After a successful upload, the form resets inline and the uploader can send another file without navigating away.
- After success: inline success message (e.g., "File received.") on the same page.
- Expired/invalid token: same branded expired-link design as `share.html` — "This link has expired or is no longer valid."
- Slot full (count limit reached): distinct "This upload slot is full." message — different from expired/invalid.
- `upload.html` follows the same branded structure as `share.html`: `BitNest Cloud` header, card layout, `style.css` shared stylesheet.

**File ownership & storage**
- Uploaded files are attributed to the slot creator (the authenticated user who owns the upload slot). `OwnerId` on `FileMetadata` is set to the slot creator's user ID.
- Uploaded files appear in the owner's file list identically to files they uploaded themselves — no special badge, section, or indicator.
- Existing chunk-based upload pipeline (`StorageService`) is reused. The public upload endpoint calls the same storage path as the authenticated upload endpoint.

**Multi-file behavior**
- One file at a time per upload action; after success the form resets, allowing another file. Each upload is a separate POST and creates a separate stored file.
- The slot accepts uploads until either the expiry date is passed or the file count limit is reached — whichever comes first.
- When the file count limit is reached, further uploads are rejected and the upload page shows the "slot full" state.

### Claude's Discretion
- Exact progress bar implementation for the public upload page (reuse XHR + progress pattern from `main.js`).
- Exact styling of the slot-full state on `upload.html`.
- API endpoint path for the public upload endpoint (e.g., `POST /api/share/{token}/upload`).
- How to surface the remaining file count to the uploader (e.g., "X of Y slots used" or "Y uploads remaining").

### Deferred Ideas (OUT OF SCOPE)
- Download count / upload count tracking visible in the links list — noted as backlog.
- Combined download+upload links — not in this phase.
- Uploader identity capture — deferred; out of scope for Phase 9.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SHRP-03 | Unauthenticated user can upload file(s) through valid sharepoint link into scoped dropbox flow | EF migration adds LinkType/Description/MaxFileCount/UploadCount; new AllowAnonymous POST endpoint delegates to StorageService.UploadFile; upload.html + upload.js follow share.html/share.js pattern |
</phase_requirements>

---

## Summary

Phase 9 extends the existing `SharepointLink` entity and its surrounding infrastructure to support a second link type: upload slots. The entire backend surface area is well-established: token generation, hash storage, expiry/revocation logic, and the `StorageService.UploadFile` pipeline are all in place and working. This phase layers new columns onto the existing table via a single EF Core migration, adds slot-creation to the existing authenticated `SharepointController`, adds a new public POST action to `PublicShareController`, and adds a new static frontend page (`upload.html` + `upload.js`) modeled on `share.html`/`share.js`.

The most consequential design decisions are already locked. The key technical risks are narrow: (1) atomic counter increment for `UploadCount` under concurrent uploads, (2) the `SharepointLink.FileId` foreign key being `NOT NULL` in the current schema — upload slots have no file — and (3) the `ValidateTokenAndGetFileAsync` return type returning a `FileMetadata` value, which is meaningless for upload slots and needs a parallel or extended method.

**Primary recommendation:** Add a new `ValidateUploadSlotAsync` method on `SharepointLinkService` that validates LinkType, expiry, revocation, and UploadCount < MaxFileCount atomically. The public upload endpoint uses only this method; `ValidateTokenAndGetFileAsync` is unchanged.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core | net9.0 | HTTP endpoint host | Locked by project stack |
| EF Core + Npgsql | 9.0.x (already in csproj) | Database migration, entity changes | Locked by project stack |
| StorageService | (project service) | Chunk-based file persistence | Reuse existing pipeline — no alternative |
| xunit + Microsoft.AspNetCore.TestHost + EF InMemory | Already in BitNest.Tests.csproj | Unit/integration tests | Established pattern from Phase 8 |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Security.Cryptography` (BCL) | net9.0 BCL | SHA256 token hashing | Same as existing `HashToken()` in `SharepointLinkService` |
| `Microsoft.AspNetCore.WebUtilities` | net9.0 BCL | `WebEncoders.Base64UrlEncode` for token generation | Same as existing `CreateLinkAsync` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Extending SharepointLink with LinkType | Separate UploadSlot entity | New entity avoids nullable FileId, but adds duplication of token/expiry logic and increases migration complexity; locked decision favors single entity |
| Separate `ValidateUploadSlotAsync` | Extending existing `ValidateTokenAndGetFileAsync` | Extending the existing method to return a discriminated union increases complexity; separate method is simpler and doesn't break Phase 8 callers |

**Installation:** No new packages required — all required packages are already in project.

---

## Architecture Patterns

### Recommended Project Structure

New files this phase introduces:

```
BitNest/
├── Migrations/
│   └── YYYYMMDD_AddUploadSlotColumns.cs        # LinkType, Description, MaxFileCount, UploadCount
├── Models/
│   └── SharepointLink.cs                       # Extended with new columns + LinkType enum
├── Services/
│   └── SharepointLinkService.cs                # ValidateUploadSlotAsync, CreateUploadSlotAsync, IncrementUploadCountAsync
├── Controllers/
│   ├── SharepointController.cs                 # Add: POST /api/sharepoint/slots, GET updated to include type/badge data
│   └── PublicShareController.cs                # Add: POST /api/share/{token}/upload, GET updated to return slot metadata
FrontEnd/
├── upload.html                                 # New branded upload page
├── upload.js                                   # New upload page JS (mirrors share.js pattern)
└── links.js / links.html                       # Extended: "New upload slot" UI, type badge column
```

### Pattern 1: LinkType Enum with Default Migration

**What:** Add `LinkType` as an `int`-backed enum column to `SharepointLinks` with `DEFAULT 0` so existing rows become `Download` (0) without data migration.
**When to use:** Any time an enum column is added to an existing table where existing rows must be non-null.

```csharp
// Source: EF Core conventions — int-backed enums stored as int columns
public enum LinkType { Download = 0, Upload = 1 }

// SharepointLink.cs additions:
public LinkType LinkType { get; set; } = LinkType.Download;
public string? Description { get; set; }
public int? MaxFileCount { get; set; }
public int UploadCount { get; set; } = 0;

// FileId becomes nullable for upload slots (no specific file)
// CRITICAL: current schema has FileId NOT NULL with FK to Files — must make nullable
public int? FileId { get; set; }
public FileMetadata? File { get; set; }
```

Migration must: (a) make `FileId` nullable, (b) drop/recreate FK with nullable column, (c) add `LinkType` int column with default 0, (d) add `Description` nvarchar nullable, (e) add `MaxFileCount` int nullable, (f) add `UploadCount` int not null default 0.

### Pattern 2: Atomic UploadCount Increment with Concurrency Check

**What:** When a valid upload arrives, verify `UploadCount < MaxFileCount` AND increment in a single operation to prevent race conditions where two simultaneous uploads both pass the check.
**When to use:** Any counter guarded by a capacity check where concurrent requests could both pass the guard.

```csharp
// Source: EF Core pessimistic/optimistic concurrency — use ExecuteUpdateAsync for atomic increment
// In SharepointLinkService:
public async Task<UploadSlotValidationResult> ValidateAndReserveUploadSlotAsync(string token)
{
    var tokenHash = HashToken(token);
    var now = DateTime.UtcNow;

    // Load slot with pessimistic lock equivalent — use row update to increment atomically
    var link = await context.SharepointLinks
        .FirstOrDefaultAsync(l => l.TokenHash == tokenHash && l.LinkType == LinkType.Upload);

    if (link == null || link.RevokedAt != null || link.ExpiresAt <= now)
        return UploadSlotValidationResult.InvalidOrExpired;

    if (link.MaxFileCount.HasValue && link.UploadCount >= link.MaxFileCount.Value)
        return UploadSlotValidationResult.SlotFull;

    // ExecuteUpdateAsync is atomic — no separate SaveChanges race window
    var updated = await context.SharepointLinks
        .Where(l => l.Id == link.Id
                 && (l.MaxFileCount == null || l.UploadCount < l.MaxFileCount))
        .ExecuteUpdateAsync(s => s.SetProperty(l => l.UploadCount, l => l.UploadCount + 1));

    if (updated == 0)
        return UploadSlotValidationResult.SlotFull; // Race: another request filled it

    return UploadSlotValidationResult.Valid(link);
}
```

**Why atomic update:** Without it, two simultaneous uploads to a slot with 1 remaining slot could both read `UploadCount < MaxFileCount`, both pass, and both store files, exceeding the limit. The `ExecuteUpdateAsync` with the guard in the `WHERE` clause makes the check-and-increment a single SQL statement.

### Pattern 3: AllowAnonymous Upload Endpoint — Token as Credential

**What:** Public multipart upload endpoint on `PublicShareController`, no `[Authorize]`, token from URL path is the sole authorization.
**When to use:** Same pattern as existing `GET /api/share/{token}` and `GET /api/share/{token}/download`.

```csharp
// Source: Existing PublicShareController.cs pattern
[HttpPost("{token}/upload")]
public async Task<IActionResult> UploadFile(string token, [FromForm] IFormFile formFile)
{
    var result = await linkService.ValidateAndReserveUploadSlotAsync(token);

    if (result == UploadSlotValidationResult.InvalidOrExpired)
        return NotFound(new { message = "This link is no longer valid" });

    if (result == UploadSlotValidationResult.SlotFull)
        return Conflict(new { message = "This upload slot is full" });

    // Attribute file to slot creator — ownerUserId from the slot
    await storageService.UploadFile(formFile, formFile.FileName,
        Path.GetExtension(formFile.FileName), result.SlotCreatorUserId);

    return Ok();
}
```

### Pattern 4: GET /api/share/{token} Extended for Upload Slots

**What:** The existing metadata endpoint must return slot-specific fields when LinkType is Upload so `upload.html` can populate the context card.
**When to use:** `upload.html` calls `GET /api/share/{token}` on load to determine (a) linkType, (b) slot metadata, (c) whether to show upload UI or expired state.

Current `GetFileMetadata` returns `fileName`, `fileSize`, `expiresAt`. Upload slots return: `linkType`, `ownerUsername`, `createdAt`, `expiresAt`, `description`, `maxFileCount`, `uploadCount` (for computing remaining slots). The endpoint dispatches on `link.LinkType` after validation — download links continue to return file info, upload slots return slot info.

**Note:** `ValidateTokenAndGetFileAsync` currently returns `(FileMetadata File, DateTime ExpiresAt)?`. For upload slots there is no File. Options: (a) add a new `ValidateTokenAndGetLinkAsync(string token)` that returns the full `SharepointLink` record — downstream callers branch on `LinkType`; or (b) keep two separate service methods. Option (a) is cleaner since it gives the controller all the data it needs in one call.

### Pattern 5: upload.html / upload.js — share.html/share.js Template

**What:** `upload.html` is structurally identical to `share.html`: branded header, loading state, content card, expired-view section. A new "slot-full" section is added.
**When to use:** Follows established `setVisible()` / `view-hidden` / `view-visible` CSS pattern.

Three view states:
1. `#loadingState` — shown on page load, hidden after metadata fetch
2. `#uploadView` — shown for valid, non-full slot: context card + dropzone
3. `#expiredView` — shown for invalid/expired/revoked token
4. `#slotFullView` — shown when slot is full (distinct from expired)

XHR upload pattern from `main.js`:
```javascript
// Source: FrontEnd/main.js uploadFile() lines 477–504
const xhr = new XMLHttpRequest();
xhr.open("POST", `${API_URL}/api/share/${encodeURIComponent(token)}/upload`, true);
// No Authorization header — token in URL path is the credential
const formData = new FormData();
formData.append("formFile", file);
xhr.upload.onprogress = event => {
    if (!event.lengthComputable) return;
    const percent = Math.round((event.loaded / event.total) * 100);
    progressBar.style.width = `${percent}%`;
    progressText.textContent = `Uploading ${percent}%`;
};
xhr.onload = () => {
    if (xhr.status >= 200 && xhr.status < 300) {
        showSuccess();   // inline success message, reset form
    } else if (xhr.status === 409) {
        showSlotFull();  // slot filled between metadata load and upload
    } else {
        showError();     // inline retry
    }
};
xhr.send(formData);
```

### Pattern 6: links.html/links.js — Upload Slot Creation UI

**What:** "New upload slot" button opens an inline creation form (not a modal, consistent with Phase 8 inline approach). The form collects: expiry (preset buttons + custom), description (optional text input), file count limit (preset buttons + custom number).
**When to use:** Same inline pattern used for other management actions on the links page.

Links list table gains a `Type` column displaying a text badge (`Download` or `Upload`). Both link types are shown in the same table. The type badge differentiates them. Upload slot rows do not have a filename (no file associated), so the file name cell shows a placeholder or "—".

### Anti-Patterns to Avoid

- **Reusing `ValidateTokenAndGetFileAsync` for upload slots without guarding LinkType:** that method returns a `FileMetadata` which is null/missing for upload slots. Use a new method or extend to return the full link.
- **Incrementing UploadCount with a read-then-write:** leads to race conditions. Always use `ExecuteUpdateAsync` with the guard in the WHERE clause.
- **Using `FileId` as NOT NULL in migration:** upload slots have no file. The migration must make `FileId` nullable before adding new rows.
- **Navigating away after upload success:** the UX spec requires inline success message and form reset, not a page redirect.
- **Showing expired-view for slot-full:** these are distinct states with different user messaging. 409 from upload = slot full. 404 from metadata = expired/invalid.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File chunking / deduplication | Custom chunking pipeline | `StorageService.UploadFile(IFormFile, ...)` | Already handles Blake3 dedup, chunk writes, metadata, `IsUploaded` flag |
| Token generation | Custom RNG | `RandomNumberGenerator.GetBytes(64)` + `WebEncoders.Base64UrlEncode` | Established pattern, cryptographically safe |
| Token hash storage | Plaintext token | SHA256 hash via `HashToken()` in `SharepointLinkService` | Established pattern, never persist raw tokens |
| Atomic counter increment under concurrency | Read-then-update with `SaveChanges` | `ExecuteUpdateAsync` with WHERE guard | Eliminates TOCTOU race for upload count limit |

**Key insight:** `StorageService.UploadFile` takes `ownerUserId` as a parameter — this is exactly the slot creator's user ID. The public endpoint does not need to authenticate as the owner; it only needs the slot's `CreatedByUserId` from the validated slot record.

---

## Common Pitfalls

### Pitfall 1: FileId NOT NULL Breaks Upload Slot Insert

**What goes wrong:** Attempting to insert a `SharepointLink` for an upload slot without a `FileId` value will fail with a DB constraint violation because the current schema has `FileId NOT NULL`.
**Why it happens:** Phase 8 assumed every link references a specific file. Upload slots are file-independent.
**How to avoid:** The migration for Phase 9 must make `FileId` nullable (`ALTER COLUMN "FileId" DROP NOT NULL`) and update the EF model property to `int? FileId`. The FK relationship in `AppDbContext` must use `.IsRequired(false)`.
**Warning signs:** Any integration test that tries to create an upload slot without a FileId will throw `DbUpdateException` if the column is still NOT NULL.

### Pitfall 2: Race Condition on UploadCount Check

**What goes wrong:** Two simultaneous uploads to a slot with `MaxFileCount = 1` both load the slot, both see `UploadCount = 0 < 1`, both proceed, and two files are stored. Slot accepts one extra file beyond limit.
**Why it happens:** EF Core's default `SaveChanges` is not atomic with a prior query.
**How to avoid:** Use `ExecuteUpdateAsync` with the capacity guard in the SQL WHERE clause. If `updated == 0`, return 409 Conflict.
**Warning signs:** Integration test with two concurrent requests to a 1-slot capacity slot fails to enforce the limit.

### Pitfall 3: Existing `GetActiveLinksForUserAsync` Returns Download Links Only by Convention

**What goes wrong:** The existing service method `GetActiveLinksForUserAsync` returns all non-expired, non-revoked links for a user. After the migration adds `LinkType`, this method returns both download and upload slot rows. The `links.js` table rendering assumes `link.fileName` is always present.
**Why it happens:** `fileName` comes from navigating to `link.File.Name`, which is null for upload slots once `FileId` is nullable.
**How to avoid:** The API response from `GET /api/sharepoint/links` must be updated to return `linkType` and handle nullable `fileName` (return `null` or omit for upload slots). `links.js` rendering must check `linkType` and show "—" for upload slot file name column.
**Warning signs:** `NullReferenceException` on `l.File.Name` in the LINQ projection if `Include(l => l.File)` doesn't eagerly load the now-nullable navigation.

### Pitfall 4: upload.html URL Routing via nginx

**What goes wrong:** `upload.html` served by nginx will work automatically since nginx uses `try_files $uri $uri.html` — a request to `/upload.html?token=...` is served the file. The token is in the query string, not the path, so no nginx rewrite is needed. However the API base URL derivation (`window.location.origin.replace("3000", "5000")`) must be correct.
**Why it happens:** `share.js` uses the same pattern and it works. `upload.js` should use the identical derivation.
**How to avoid:** Copy the `API_URL` derivation line verbatim from `share.js`.
**Warning signs:** Upload requests going to the wrong port in development (3000 instead of 5000).

### Pitfall 5: Returning 410 Gone vs 404 for Expired/Revoked Tokens

**What goes wrong:** The existing `PublicShareController` returns 404 for invalid/expired tokens. Frontend `upload.js` should handle both 404 and 410 as "expired/invalid" and show `expiredView`. Returning 409 only for slot-full is the important distinction to test.
**Why it happens:** HTTP semantics: 410 Gone is more accurate for expired links, but the existing Phase 8 pattern uses 404.
**How to avoid:** Match the existing Phase 8 pattern (404) for expired/invalid to avoid frontend branching inconsistency. Use 409 Conflict exclusively for slot-full.
**Warning signs:** Frontend showing wrong view state if response code mapping is inconsistent with Phase 8.

---

## Code Examples

Verified patterns from project source:

### Existing Token Hash Pattern (reuse unchanged)
```csharp
// Source: BitNest/Services/SharepointLinkService.cs HashToken()
private string HashToken(string token)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    return Convert.ToBase64String(bytes);
}
```

### Existing Upload Pipeline Entry Point
```csharp
// Source: BitNest/Services/StorageService.cs UploadFile()
// ownerUserId parameter is the slot creator's ID for public uploads
public async Task<string?> UploadFile(IFormFile formFile, string fileName, string extension, int ownerUserId = 0)
```

### Existing XHR Upload Pattern (adapt for upload.js — remove auth header)
```javascript
// Source: FrontEnd/main.js lines 477–504
const xhr = new XMLHttpRequest();
xhr.open("POST", `${API_URL}/Storage`, true);
if (authState.accessToken) xhr.setRequestHeader("Authorization", `Bearer ${authState.accessToken}`);
xhr.upload.onprogress = event => { /* progress bar update */ };
xhr.onload = () => { /* handle 401, 2xx */ };
xhr.send(formData);
// upload.js: remove auth header line; handle 409 for slot-full; handle 404 on metadata load
```

### Existing view-state pattern (copy into upload.js)
```javascript
// Source: FrontEnd/share.js
function setVisible(el, visible) {
    if (!el) return;
    el.classList.toggle('view-hidden', !visible);
    el.classList.toggle('view-visible', visible);
}
```

### Existing Links List Table Rendering (extend with type badge column)
```javascript
// Source: FrontEnd/links.js loadLinks() — table columns to extend
// Add "Type" <th> and per-row <td> with badge
// Add "New Upload Slot" button before table, wired to inline creation form
```

### EF Core ExecuteUpdateAsync for Atomic Counter Increment
```csharp
// Source: EF Core 7+ API — confirmed available in net9.0 EF Core 9.x
var updated = await context.SharepointLinks
    .Where(l => l.Id == link.Id
             && (l.MaxFileCount == null || l.UploadCount < l.MaxFileCount))
    .ExecuteUpdateAsync(s => s.SetProperty(l => l.UploadCount, l => l.UploadCount + 1));
// updated == 0 means slot just filled (concurrent request won the race)
```

### Migration Shape (conceptual)
```csharp
// Make FileId nullable
migrationBuilder.AlterColumn<int>("FileId", "SharepointLinks", nullable: true, oldNullable: false);
// Add LinkType (int, default 0 = Download)
migrationBuilder.AddColumn<int>("LinkType", "SharepointLinks", defaultValue: 0, nullable: false);
// Add Description (text, nullable)
migrationBuilder.AddColumn<string>("Description", "SharepointLinks", nullable: true);
// Add MaxFileCount (int, nullable)
migrationBuilder.AddColumn<int>("MaxFileCount", "SharepointLinks", nullable: true);
// Add UploadCount (int, default 0, not null)
migrationBuilder.AddColumn<int>("UploadCount", "SharepointLinks", defaultValue: 0, nullable: false);
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ValidateTokenAndGetFileAsync` returns `(FileMetadata, DateTime)?` | Needs new `ValidateAndReserveUploadSlotAsync` returning full `SharepointLink` | Phase 9 | Download endpoint unchanged; new method added alongside |
| Download-only link type | `LinkType` enum: `Download=0`, `Upload=1` | Phase 9 migration | Existing rows default to 0, no data migration needed |
| `SharepointLink.FileId` NOT NULL | Nullable `FileId` | Phase 9 migration | Upload slots have no file; nullable FK required |

---

## Open Questions

1. **CreateLinkAsync signature change for upload slots**
   - What we know: existing `CreateLinkAsync(int fileId, int userId, DateTime expiresAt, string baseUrl)` requires a fileId and builds a `ShareUrl` pointing to `share.html?token=...`.
   - What's unclear: upload slot creation needs a different factory — no fileId, builds `upload.html?token=...` ShareUrl, needs `Description` and `MaxFileCount`.
   - Recommendation: add `CreateUploadSlotAsync(int userId, DateTime expiresAt, string? description, int? maxFileCount, string baseUrl)` as a separate service method. Do not modify the existing method.

2. **`GetActiveLinksForUserAsync` LINQ projection includes `l.File.Name`**
   - What we know: after making `FileId` nullable, `l.File` will be null for upload slots even with `Include(l => l.File)`.
   - What's unclear: whether the current `GetActiveLinksForUserAsync` is used by the links list endpoint directly, or whether the controller projects it.
   - Recommendation: update the `GET /api/sharepoint/links` controller action to handle null File navigation: `fileName = l.File?.Name` in the anonymous type projection. Also add `linkType`, `description`, `maxFileCount`, `uploadCount` to the response shape.

3. **UploadCount visibility on upload.html context card**
   - What we know: Claude's Discretion — show remaining count or used/total, not specified.
   - Recommendation: show `"X uploads remaining"` where X = `MaxFileCount - UploadCount`. If `MaxFileCount` is null (unlimited), do not show a count at all.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 + Microsoft.AspNetCore.TestHost 9.0.4 + EF InMemory 9.0.4 |
| Config file | `BitNest.Tests/BitNest.Tests.csproj` |
| Quick run command | `dotnet test --filter "Category=SharepointUploadSlots" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SHRP-03 | Upload slot creation stores correct entity (LinkType=Upload, Description, MaxFileCount, UploadCount=0) | unit | `dotnet test --filter "FullyQualifiedName~CreateUploadSlotAsync"` | ❌ Wave 0 |
| SHRP-03 | ValidateAndReserveUploadSlotAsync returns Valid for valid non-full slot | unit | `dotnet test --filter "FullyQualifiedName~ValidateAndReserveUploadSlotAsync"` | ❌ Wave 0 |
| SHRP-03 | ValidateAndReserveUploadSlotAsync returns SlotFull when UploadCount >= MaxFileCount | unit | `dotnet test --filter "FullyQualifiedName~ValidateAndReserveUploadSlotAsync"` | ❌ Wave 0 |
| SHRP-03 | ValidateAndReserveUploadSlotAsync returns InvalidOrExpired for expired token | unit | `dotnet test --filter "FullyQualifiedName~ValidateAndReserveUploadSlotAsync"` | ❌ Wave 0 |
| SHRP-03 | ValidateAndReserveUploadSlotAsync rejects download link token (wrong type) | unit | `dotnet test --filter "FullyQualifiedName~ValidateAndReserveUploadSlotAsync"` | ❌ Wave 0 |
| SHRP-03 | POST /api/share/{token}/upload returns 200 and stored file attributed to slot owner | integration | `dotnet test --filter "FullyQualifiedName~UploadFile_stores_file_attributed_to_slot_owner"` | ❌ Wave 0 |
| SHRP-03 | POST /api/share/{token}/upload returns 409 when slot is full | integration | `dotnet test --filter "FullyQualifiedName~UploadFile_returns_409_when_slot_full"` | ❌ Wave 0 |
| SHRP-03 | POST /api/share/{token}/upload returns 404 for expired token | integration | `dotnet test --filter "FullyQualifiedName~UploadFile_returns_404_for_expired_token"` | ❌ Wave 0 |
| SHRP-04 | POST /api/share/{token}/upload returns 404 for revoked token | integration | `dotnet test --filter "FullyQualifiedName~UploadFile_returns_404_for_revoked_token"` | ❌ Wave 0 |
| SHRP-03 | GET /api/sharepoint/links returns both download and upload slot rows with correct type field | integration | `dotnet test --filter "FullyQualifiedName~GetLinks_returns_both_link_types"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "Category=SharepointUploadSlots" --no-build`
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `BitNest.Tests/Services/SharepointUploadSlotServiceTests.cs` — covers `CreateUploadSlotAsync`, `ValidateAndReserveUploadSlotAsync` (all states)
- [ ] `BitNest.Tests/Controllers/PublicUploadControllerTests.cs` — covers `POST /api/share/{token}/upload` (200, 404, 409)
- [ ] `BitNest.Tests/Controllers/SharepointControllerUploadSlotTests.cs` — covers slot creation endpoint and updated `GET /api/sharepoint/links` response shape

---

## Sources

### Primary (HIGH confidence)

- Direct source inspection: `BitNest/Models/SharepointLink.cs` — current entity schema; `FileId` is `int` NOT NULL
- Direct source inspection: `BitNest/Services/SharepointLinkService.cs` — token generation, validation, and hash patterns
- Direct source inspection: `BitNest/Controllers/PublicShareController.cs` — AllowAnonymous + token-as-credential pattern
- Direct source inspection: `BitNest/Controllers/SharepointController.cs` — authenticated CRUD endpoint pattern
- Direct source inspection: `BitNest/Controllers/StorageController.cs` + `BitNest/Services/StorageService.cs` — `UploadFile(IFormFile, string, string, int ownerUserId)` signature
- Direct source inspection: `FrontEnd/share.js` + `FrontEnd/share.html` — view-state pattern, `setVisible()`, loading/expired view structure
- Direct source inspection: `FrontEnd/main.js` lines 477–504 — XHR upload with progress, FormData, no-auth variant needed
- Direct source inspection: `FrontEnd/links.js` + `FrontEnd/links.html` — existing links list table, copy/revoke button pattern
- Direct source inspection: `BitNest/Data/AppDbContext.cs` — EF config, FK delete behaviors, index patterns
- Direct source inspection: `FrontEnd/nginx.conf` — `try_files $uri $uri.html` confirms `upload.html` needs no nginx changes
- Direct source inspection: `BitNest.Tests/Services/SharepointLinkServiceTests.cs` + `BitNest.Tests/Controllers/PublicShareControllerTests.cs` — test patterns, InMemory setup, Trait annotations

### Secondary (MEDIUM confidence)

- EF Core `ExecuteUpdateAsync` atomic increment pattern — confirmed available in EF Core 7+ (project uses EF Core 9.x)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already present in project; no new dependencies
- Architecture: HIGH — all patterns directly observed in Phase 8 source code
- Pitfalls: HIGH — identified from direct schema inspection (FileId NOT NULL, ValidateTokenAndGetFileAsync return type)
- Concurrency pattern: MEDIUM — `ExecuteUpdateAsync` pattern is correct for EF Core 7+; confirmed available but not a pattern already used in this codebase

**Research date:** 2026-03-20
**Valid until:** 2026-04-20 (stable stack, 30-day window)

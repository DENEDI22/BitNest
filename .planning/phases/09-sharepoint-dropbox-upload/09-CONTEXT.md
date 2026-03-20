# Phase 9: Sharepoint Dropbox Upload - Context

**Gathered:** 2026-03-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Add scoped third-party upload flow using valid sharepoint links. Unauthenticated users can upload files into an owner's storage space via a valid, non-expired upload slot. The uploaded files are owned by the slot creator and appear in their file list. Phase 8 download links are unchanged. Creating, listing, and revoking upload slots is managed from the existing #links page.

</domain>

<decisions>
## Implementation Decisions

### Link type — upload slots vs download links
- Upload slots are a separate link type, not tied to any specific file. They are generic dropbox-style drop zones into the owner's storage space.
- The same `SharepointLink` entity is used, with a new `LinkType` enum column (`Download` | `Upload`). A migration adds the column with `Download` as the default for existing rows.
- Upload slots are created from the `#links` page via a "New upload slot" action — not from a per-file Share button.
- The `#links` page shows all active links (both types) in one unified list. Each row has a type badge indicating `Download` or `Upload`.
- Expiry presets are the same as download links (1 hour, 24 hours, 7 days, 30 days, custom date/time).

### Upload slot creation
- Owner provides: expiry (required, same presets as download links) and optional description/label (e.g., "Photos from the event") shown to the uploader.
- Owner sets a file count limit at creation time (required). Presets: 1, 5, 10, 25 files — plus a custom number input.
- Description and file limit are stored on the `SharepointLink` entity (new nullable `Description` and nullable int `MaxFileCount` columns).

### Upload UX — public upload page
- A separate `upload.html` page (distinct from `share.html`) served at a separate URL path (e.g., `/upload?token=...`).
- Page shows a full context card: owner username, slot creation date, expiry, optional description, remaining file count (if limit not yet hit), and a dropzone/file picker.
- Upload is one file at a time. After a successful upload, the form resets inline and the uploader can send another file without navigating away.
- After success: inline success message (e.g., "File received.") on the same page.
- Expired/invalid token: same branded expired-link design as `share.html` — "This link has expired or is no longer valid."
- Slot full (count limit reached): distinct "This upload slot is full." message — different from expired/invalid, so the uploader understands the distinction.
- `upload.html` follows the same branded structure as `share.html`: `BitNest Cloud` header, card layout, `style.css` shared stylesheet.

### File ownership & storage
- Uploaded files are attributed to the slot creator (the authenticated user who owns the upload slot). `OwnerId` on `FileMetadata` is set to the slot creator's user ID.
- Uploaded files appear in the owner's file list identically to files they uploaded themselves — no special badge, section, or indicator.
- Existing chunk-based upload pipeline (`StorageService`) is reused. The public upload endpoint calls the same storage path as the authenticated upload endpoint.

### Multi-file behavior
- One file at a time per upload action; after success the form resets, allowing another file. Each upload is a separate POST and creates a separate stored file.
- The slot accepts uploads until either the expiry date is passed or the file count limit is reached — whichever comes first.
- When the file count limit is reached, further uploads are rejected and the upload page shows the "slot full" state.

### Claude's Discretion
- Exact progress bar implementation for the public upload page (reuse XHR + progress pattern from `main.js`).
- Exact styling of the slot-full state on `upload.html`.
- API endpoint path for the public upload endpoint (e.g., `POST /api/share/{token}/upload`).
- How to surface the remaining file count to the uploader (e.g., "X of Y slots used" or "Y uploads remaining").

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope and requirements
- `.planning/ROADMAP.md` — Phase 9 goal, success criteria (SHRP-03), and plan outline (09-01 backend, 09-02 frontend).
- `.planning/REQUIREMENTS.md` — Authoritative requirement SHRP-03 for this phase.
- `.planning/PROJECT.md` — Milestone constraints, stack constraints, and active feature boundary.

### Prior locked context
- `.planning/phases/08-sharepoint-expiring-download-links/08-CONTEXT.md` — Established SharepointLink entity structure, token hash pattern, #links page layout, share.html page pattern, copy-to-clipboard pattern, and expired-link page design. Phase 9 extends all of these.
- `.planning/phases/07-user-management-and-file-access-enforcement/07-CONTEXT.md` — Hash routing pattern (#links, #admin, #files), per-file action row pattern, header nav structure.
- `.planning/phases/06-identity-and-session-foundation/06-CONTEXT.md` — JWT/auth patterns; public endpoints use AllowAnonymous with token-as-credential (no auth header).
- `.planning/STATE.md` — Accumulated phase decisions and current continuity notes.

### Codebase baseline and conventions
- `.planning/codebase/STRUCTURE.md` — Key file locations and integration points.
- `.planning/codebase/CONVENTIONS.md` — Controller/service/frontend patterns to preserve.
- `.planning/codebase/STACK.md` — Stack/runtime constraints.
- `.planning/codebase/INTEGRATIONS.md` — API/frontend/proxy integration context.

No external ADR/spec documents were referenced during discussion; requirements are fully captured in the docs above and decisions in this context.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BitNest/Controllers/PublicShareController.cs` — Existing AllowAnonymous controller with token validation pattern (`ValidateTokenAndGetFileAsync`). Phase 9 public upload endpoint extends this or follows the same structure.
- `BitNest/Services/SharepointLinkService.cs` — `ValidateTokenAndGetFileAsync` is the token validation path; needs extension to validate upload slots and check file count.
- `BitNest/Controllers/SharepointController.cs` — Authenticated link management (create, list, revoke). Phase 9 adds upload slot creation here, plus endpoints to surface slot metadata (description, file count) for the public upload page.
- `BitNest/Controllers/StorageController.cs` — Existing chunk-based upload path; public upload endpoint can delegate to the same `StorageService` upload logic.
- `BitNest/Models/SharepointLink.cs` — Needs `LinkType` enum column, nullable `Description` string, nullable int `MaxFileCount`, and an `UploadCount` counter (incremented on each successful upload through the slot).
- `FrontEnd/share.html` + `FrontEnd/share.js` — Template for `upload.html` structure: branded header, card layout, loading/content/expired view states, `setVisible()` utility, `style.css`.
- `FrontEnd/main.js` `uploadFile()` — XHR upload with progress bar; the upload.html upload function follows the same pattern (minus auth headers, using token in path instead).
- `FrontEnd/links.html` + `FrontEnd/links.js` — #links management page that needs the "New upload slot" creation button and type badge column on the existing links list.

### Established Patterns
- **AllowAnonymous + token-as-credential**: public endpoints on `PublicShareController` use the token from the URL path as sole authorization — no `Authorization` header.
- **Token hash validation**: `ValidateTokenAndGetFileAsync` hashes the incoming token and looks up `TokenHash` — same pattern needed for upload slot validation.
- **Thin controller + service delegation**: all new upload slot logic goes in `SharepointLinkService`, not in the controller.
- **Hash routing**: `#links` view in `links.html` follows the existing `routeToHash()` dispatch pattern.
- **XHR with progress**: `main.js` `uploadFile()` uses XHR with `xhr.upload.onprogress` for the progress bar — identical pattern for `upload.html`.
- **View state management**: `setVisible()` / `view-hidden` / `view-visible` CSS class toggles — same approach in `upload.html`.

### Integration Points
- `SharepointLink` migration: add `LinkType` (enum stored as int), `Description` (nvarchar, nullable), `MaxFileCount` (int, nullable), `UploadCount` (int, default 0) columns.
- New public endpoint: `POST /api/share/{token}/upload` (AllowAnonymous) — validates token is an upload slot, checks expiry and file count, stores file via StorageService, increments UploadCount, returns 200 or 409/410.
- `GET /api/share/{token}` response needs to return slot metadata (type, description, maxFileCount, uploadCount) so `upload.html` can render the context card.
- `SharepointController.CreateLink` extended (or a new `POST /api/sharepoint/slots` action) to accept `LinkType`, `Description`, and `MaxFileCount` in the request body.
- `links.html` / `links.js`: add "New upload slot" button and a creation form; add `type` badge to the list rows.
- New `upload.html` + `upload.js` served by the frontend container (nginx), following `share.html` as structural template.

</code_context>

<specifics>
## Specific Ideas

- The "slot full" state on `upload.html` should be distinct from the "expired/invalid" state — the uploader needs to understand their file was not rejected due to token problems but because the slot capacity was reached.
- Upload slots are not tied to specific files — they are generic drop zones into the owner's space. This is the key difference from download links (which point at a specific `FileMetadata` record).
- The `upload.html` context card should show owner username (not just "BitNest") so the uploader knows whose space they're contributing to.

</specifics>

<deferred>
## Deferred Ideas

- Download count / upload count tracking visible in the links list — noted as backlog (download count was already deferred in Phase 8; upload count per slot could be a future addition to the Links UI).
- Combined download+upload links (a link that lets recipients both download a file and drop new ones) — not in this phase; would require a new link type variant.
- Uploader identity capture (e.g., optional "your name" field on upload page) — deferred; out of scope for Phase 9.

</deferred>

---

*Phase: 09-sharepoint-dropbox-upload*
*Context gathered: 2026-03-20*

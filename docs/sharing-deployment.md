# Private/Public Split

## Boundaries

`BitNest` is the private API. `frontend` contains only the private index, admin and
link-management pages. Its published port binds to loopback by default; set
`PRIVATE_BIND_IP` to this machine's Tailscale address to allow tailnet access.
The API binds only its private Docker network address, not its management-network
address. Neither PostgreSQL database publishes a host port.

`BitNest.Sharing` is a separate executable and image. Both services use the single
PostgreSQL container, but as separate databases with separate roles: `bitnest` owns
the private database and `sharing` owns the `sharing` database, which contains only
`Links`, `Incoming`, `RevokedFiles` and EF migration history. The separation is
logical, enforced by PostgreSQL roles, not by separate servers. The sharing role
cannot connect to the private database. The sharing service has no reference to the
private application, user/password/session/grant tables, private database
credentials, or private storage volume. `BitNest.Sharing.Contracts` contains only
wire DTOs and is the only shared application dependency.

`db/init/10-sharing-database.sh` creates the sharing role and database when the
PostgreSQL volume is first initialised. On an existing volume run it by hand once:

```sh
docker compose exec -T -e SHARING_DB_PASSWORD="$SHARING_DB_PASSWORD" db sh -s < db/init/10-sharing-database.sh
```

The sharing executable has two listeners:

- Port 8080: only `/api/share/{token}` and its download/upload routes.
- Port 8081: only `/internal/*`, requiring `X-Sharing-Key` on every request.

The internal listener binds only the management network interface in Compose
(loopback outside Compose). The listener check uses the actual local socket port,
not `Host` or forwarded headers. Even a correct key cannot invoke management on
the public listener. No management port is published to the host. The database
container sits on its own internal network reachable by both services and nothing
else. The public Nginx container is not attached to the management, database or
private networks, and proxies only
`/api/share/` to port 8080. All other non-public paths return 404. It contains no
private HTML or JavaScript. Public requests never proxy to the private API.

## Deployment

Set the variables documented in `.env.example` in a local `.env` or your deployment
environment. Use independent, randomly generated values for both database role
passwords, the JWT signing key and management key. Keep `.env` out of version
control. Existing database volumes keep their existing passwords; changing the
environment alone does not rotate an initialized PostgreSQL account.

```sh
docker compose config --quiet
docker compose up -d --build
```

Configure your Internet-facing TLS proxy or Tailscale Funnel to forward **only**
to `127.0.0.1:3001` (the public frontend), never port 3000 or an API container.
Set `SHARING_PUBLIC_URL` to the externally reachable HTTPS origin. URL generation
does not trust the private browser's Host header. Terminate TLS before this Nginx
listener; do not expose plain HTTP bearer links on the Internet. Disable or redact
query strings and `/api/share/{token}` paths in that outer proxy's access logs.
The supplied public Nginx disables access logs and emits `no-store` and
`Referrer-Policy: no-referrer`; the sharing API suppresses routine ASP.NET access logs.

Private browser traffic is same-origin through the private Nginx proxy. The old
browser API paths (`/Storage`, `/auth`, `/admin/users`, `/api/sharepoint`) remain.
Public `share.html?token=...`, `upload.html?token=...` and `/api/share/...` paths
remain, on the separate public origin. The private API is no longer a frontend
reverse proxy and port 5000 is no longer published by Compose.

For non-Compose deployment, configure `Private:ListenHost` (default 127.0.0.1),
`Private:Port` (8080), `Sharing:InternalUrl` and `Sharing:ManagementKey` on the private
API. On the sharing service configure `ConnectionStrings:Sharing`,
`Sharing:ObjectsPath`, `Sharing:PublicUrl`, `Sharing:ManagementKey`, and optionally
`Sharing:PublicPort`, `Sharing:InternalPort`, `Sharing:InternalHost`. Use a private
encrypted transport (Tailscale or TLS) if management crosses machines. Never bind
management to an Internet-facing interface. The supplied Compose network aliases
select interfaces without fixed IP/subnet assignments.

## Data Lifecycle

Publishing checks private ownership or a file grant and requires an uploaded,
non-deleted private file. The private API streams a complete copy to the sharing
object store. The receiving side computes whole-file BLAKE3, verifies the expected
hash, and installs the object before creating a publication snapshot. Private
storage is never mounted or used as a public download fallback.

Public uploads stream into a temporary file, compute whole-file BLAKE3, flush to
disk and atomically install the object. Only then does one PostgreSQL transaction
increment the slot count and create an incoming queue record. Concurrent uploads
cannot exceed the slot count. A successful response means the public service has
accepted the file durably, **not** that the private instance has imported it yet.
Accepted files remain queued during private downtime and after slot expiry or
revocation. Failed uploads do not consume a slot. A response lost after commit can
cause a browser retry to consume another slot; browser submission deduplication is
not implemented.

Slot provisioning first commits a private owner mapping, then sends its stable
provisioning ID to the public service. Public provisioning is idempotent for that
ID. A lost response after public commit cannot orphan accepted uploads; a failed
public request may leave an unused private mapping, but never a slot without an
import destination. The active-link listing can recover a committed public link.

The private worker polls every ten seconds and streams accepted objects into its
own BLAKE3 storage. It resolves ownership from a private `UploadSlotOwners` mapping,
not a user ID asserted by the public inbox, checks that owner is active, and verifies
length/hash. `Files.IncomingUploadId` is a unique durable receipt inserted with file
metadata. Only after that insert commits does it acknowledge the public queue.
Lost acknowledgements, worker restarts and concurrent workers do not create another
private file. Receipts survive soft deletion, so retries cannot resurrect deleted
imports. Failed imports remain queued and logged; pagination skips retained failures
within a polling pass rather than letting the first failed batch starve later files.

Revoking a link blocks subsequent public requests. Deleting a private file first
revokes **all** public links for that source file, including links issued by granted
users, and records a permanent source-file tombstone. Publication and revocation
serialize on the source file ID; an in-flight publication cannot undo deletion.
Private deletion fails closed if sharing is unavailable or does not confirm
revocation. If private persistence fails after public revocation, the private file
remains but links stay revoked; retry deletion. Already-open downloads may finish.
Accepted incoming uploads are not cancelled by revoking their upload slot.

## Upgrade And Operations

Back up the private database and object volume before upgrading. Existing
whole-file storage and migrations are preserved. Both executables apply their own
EF migrations on startup. The new private migrations add slot-owner mappings and
import receipts; it does not remove old private sharing rows.

Existing links in the old private `SharepointLinks` table are **not automatically
published**. They cease working when the old public controller is removed. Reissue
needed links through the new private UI. This avoids copying the entire library or
silently extending old public access. Do not leave the old all-in-one deployment
publicly reachable alongside the new deployment.

Treat a private deployment and its sharing database as a paired identity. Both
live in the one `pgdata` volume, so a volume backup captures them together; back
up the two object volumes alongside it. Do not replace the sharing database with an
empty one while retaining private slot mappings, or reuse a sharing database with
unrelated private file IDs. The current protocol is
single-private-instance, not multi-tenant federation.

The default public object/upload limit is 1 GiB, including objects published by the
private API. Change `Sharing:MaxUploadBytes` and public Nginx's `client_max_body_size`
together if needed. Total storage quotas, malware scanning, automatic object garbage
collection, queue dead-letter administration, and automatic key rotation are not
implemented. Revoked/expired objects, acknowledged incoming bytes and crash-orphaned
objects remain on public disk, but have no public object-by-hash route. Plan disk
monitoring and retention procedures. File flushes and atomic renames protect process
crash recovery; host power-loss guarantees still depend on filesystem/storage and
database durability settings. The public process is trusted with published data,
not the private library. A host/Docker-daemon compromise is outside this separation.

## Verification

```sh
dotnet build BitNest.sln -c Release
dotnet test BitNest.sln
```

The PostgreSQL workflow tests skip unless `BITNEST_TEST_POSTGRES` is set. Point it
at a **disposable** PostgreSQL server where the supplied user can create databases:

```sh
BITNEST_TEST_POSTGRES='Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=...' dotnet test BitNest.sln
```

Tests create randomly named private/public databases, migrate both, exercise real
HTTP routes through TestServer and PostgreSQL transactions, and drop their databases
afterward. No production database should be used for this command.

`BitNest.Tests/Sharing/compose-smoke.mjs` exercises the actual frontend proxies,
private authentication, publications, uploads, background import and revocation.
Run it only against a disposable deployment (default URLs are localhost:3300 and
localhost:3301), supplying `BITNEST_SMOKE_ADMIN_PASSWORD`. Optional
`BITNEST_SMOKE_RESTART_CONTAINERS=1` stops/restarts the specifically named
`bitnest-split-smoke-api-1` and `bitnest-split-smoke-sharing-1` containers to test
acceptance during private downtime and queue survival through a public restart.

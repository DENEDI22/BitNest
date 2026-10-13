import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";

// Run only against a disposable deployment: this creates and soft-deletes test files.
const privateUrl = process.env.BITNEST_SMOKE_PRIVATE_URL ?? "http://localhost:3300";
const publicUrl = process.env.BITNEST_SMOKE_PUBLIC_URL ?? "http://localhost:3301";
const password = process.env.BITNEST_SMOKE_ADMIN_PASSWORD;
assert.ok(password, "BITNEST_SMOKE_ADMIN_PASSWORD is required");

async function request(base, path, options = {}, expected = 200) {
    const response = await fetch(`${base}${path}`, options);
    assert.equal(response.status, expected, `${options.method ?? "GET"} ${path}: ${await response.clone().text()}`);
    return response;
}

const login = await (await request(privateUrl, "/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username: "admin", password })
})).json();
const auth = { Authorization: `Bearer ${login.accessToken}` };
const jsonHeaders = { ...auth, "Content-Type": "application/json" };
const payload = "BitNest split container workflow bytes";
const filename = `split-smoke-${Date.now()}.txt`;
const form = new FormData();
form.append("formFile", new Blob([payload]), filename);
await request(privateUrl, "/Storage", { method: "POST", headers: auth, body: form });
const files = await (await request(privateUrl, "/Storage/1", { headers: auth })).json();
const original = files.find(file => file.FileName === filename);
assert.ok(original, "Uploaded private file must be listed");
const expiresAt = new Date(Date.now() + 3600000).toISOString();
const link = await (await request(privateUrl, "/api/sharepoint/links", {
    method: "POST", headers: jsonHeaders, body: JSON.stringify({ fileId: original.Id, expiresAt })
}, 201)).json();
assert.equal(new URL(link.url).origin, publicUrl);
const token = new URL(link.url).searchParams.get("token");
assert.equal(await (await request(publicUrl, `/api/share/${token}/download`)).text(), payload);
await request(publicUrl, "/share.html");
await request(publicUrl, "/upload.html");
for (const path of ["/", "/index.html", "/main.js", "/admin.html", "/links.js", "/auth/me", "/Storage/1", "/internal/incoming", "/api/sharepoint/links"]) {
    await request(publicUrl, path, {}, 404);
}
await request(privateUrl, `/api/share/${token}`, {}, 404);

const slot = await (await request(privateUrl, "/api/sharepoint/slots", {
    method: "POST", headers: jsonHeaders,
    body: JSON.stringify({ expiresAt, description: "container smoke", maxFileCount: 1 })
}, 201)).json();
const slotToken = new URL(slot.url).searchParams.get("token");
const incomingName = `incoming-${filename}`;
const incoming = new FormData();
incoming.append("formFile", new Blob([payload]), incomingName);
const restart = process.env.BITNEST_SMOKE_RESTART_CONTAINERS === "1";
if (restart) execFileSync("docker", ["stop", "bitnest-split-smoke-api-1"]);
try {
    await request(publicUrl, `/api/share/${slotToken}/upload`, { method: "POST", body: incoming });
    await request(publicUrl, `/api/share/${slotToken}/upload`, { method: "POST", body: incoming }, 409);
    if (restart) execFileSync("docker", ["restart", "bitnest-split-smoke-sharing-1"]);
} finally {
    if (restart) {
        execFileSync("docker", ["start", "bitnest-split-smoke-api-1"]);
        await new Promise(resolve => setTimeout(resolve, 3000));
    }
}
let imported;
for (let attempt = 0; attempt < 30; attempt++) {
    const current = await (await request(privateUrl, "/Storage/1", { headers: auth })).json();
    imported = current.find(file => file.FileName === incomingName);
    if (imported) break;
    await new Promise(resolve => setTimeout(resolve, 1000));
}
assert.ok(imported, "Private background worker must import the public upload");
assert.equal(await (await request(privateUrl, `/Storage/download/${imported.Id}`, { headers: auth })).text(), payload);
await request(privateUrl, `/Storage/${original.Id}`, { method: "DELETE", headers: auth }, 204);
await request(publicUrl, `/api/share/${token}/download`, {}, 404);
await request(privateUrl, `/api/sharepoint/links/${slot.id}`, { method: "DELETE", headers: auth }, 204);
await request(publicUrl, `/api/share/${slotToken}`, {}, 404);
await request(privateUrl, `/Storage/${imported.Id}`, { method: "DELETE", headers: auth }, 204);
console.log("Compose smoke passed: private auth/storage, independent publication, public route allowlist, worker import, slot capacity and revocation.");
if (restart) console.log("Durability passed: accepted upload while private API was stopped, restarted sharing, then imported after private API restart.");

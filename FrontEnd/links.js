const API_URL = window.location.origin.replace("3000", "5000");
const REFRESH_LOCAL_KEY = "bitnest.refresh.local";
const REFRESH_SESSION_KEY = "bitnest.refresh.session";

const authState = {
    accessToken: "",
    refreshToken: "",
    rememberMe: false,
    tokenExpiresAt: 0,
};

function parseJwtExpiry(token) {
    try {
        const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
        return (payload.exp || 0) * 1000;
    } catch { return 0; }
}

// ─── Auth helpers ────────────────────────────────────────────────────────────

function persistRefreshToken(token, rememberMe) {
    try {
        window.localStorage.removeItem(REFRESH_LOCAL_KEY);
        window.sessionStorage.removeItem(REFRESH_SESSION_KEY);
        if (!token) return;
        if (rememberMe) {
            window.localStorage.setItem(REFRESH_LOCAL_KEY, token);
        } else {
            window.sessionStorage.setItem(REFRESH_SESSION_KEY, token);
        }
    } catch { /* ignore */ }
}

function readPersistedRefreshToken() {
    try {
        const local = window.localStorage.getItem(REFRESH_LOCAL_KEY);
        if (local) { authState.refreshToken = local; authState.rememberMe = true; return local; }
        const session = window.sessionStorage.getItem(REFRESH_SESSION_KEY);
        if (session) { authState.refreshToken = session; authState.rememberMe = false; return session; }
    } catch { return authState.refreshToken; }
    return "";
}

function clearPersistedRefreshToken() {
    try {
        window.localStorage.removeItem(REFRESH_LOCAL_KEY);
        window.sessionStorage.removeItem(REFRESH_SESSION_KEY);
    } catch { /* ignore */ }
}

function resetAuthState() {
    authState.accessToken = "";
    authState.refreshToken = "";
    authState.rememberMe = false;
    authState.tokenExpiresAt = 0;
    clearPersistedRefreshToken();
}

function authHeaders() {
    const headers = new Headers();
    if (authState.accessToken) headers.set("Authorization", `Bearer ${authState.accessToken}`);
    return headers;
}

async function readJsonSafe(response) {
    try { return await response.json(); } catch { return null; }
}

async function fetchWithAuth(url, options = {}) {
    options.headers = authHeaders();
    let response = await fetch(url, options);
    if (response.status === 401) {
        const refreshed = await refreshSession();
        if (!refreshed) { window.location.href = "index.html"; return null; }
        options.headers = authHeaders();
        response = await fetch(url, options);
        if (response.status === 401) { window.location.href = "index.html"; return null; }
    }
    return response;
}

async function refreshSession() {
    const token = authState.refreshToken || readPersistedRefreshToken();
    if (!token) return false;
    const response = await fetch(`${API_URL}/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: token })
    });
    if (!response.ok) { resetAuthState(); return false; }
    const tokens = await readJsonSafe(response);
    if (!tokens?.accessToken) { resetAuthState(); return false; }
    authState.accessToken = tokens.accessToken;
    authState.refreshToken = tokens.refreshToken;
    authState.tokenExpiresAt = parseJwtExpiry(tokens.accessToken);
    persistRefreshToken(tokens.refreshToken, authState.rememberMe);
    return true;
}

async function logout() {
    const token = authState.refreshToken || readPersistedRefreshToken();
    if (token) {
        await fetch(`${API_URL}/auth/logout`, {
            method: "POST",
            headers: { "Content-Type": "application/json", ...Object.fromEntries(authHeaders()) },
            body: JSON.stringify({ refreshToken: token })
        }).catch(() => {});
    }
    resetAuthState();
    window.location.href = "index.html";
}

// ─── View state helper ────────────────────────────────────────────────────────

function setVisible(el, visible) {
    if (!el) return;
    el.classList.toggle('view-hidden', !visible);
    el.classList.toggle('view-visible', visible);
}

// ─── Links logic ─────────────────────────────────────────────────────────────

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

async function loadLinks() {
    const container = document.getElementById("linksListContainer");
    container.innerHTML = '<p class="muted" style="padding:20px">Loading...</p>';

    const response = await fetchWithAuth(`${API_URL}/api/sharepoint/links`);
    if (!response) return;
    if (!response.ok) { container.innerHTML = '<p class="error" style="padding:20px">Failed to load links.</p>'; return; }

    const links = await readJsonSafe(response);

    if (!links || links.length === 0) {
        container.innerHTML = '<p class="empty-state">No active links. Share a file or create an upload slot to get started.</p>';
        return;
    }

    const table = document.createElement("table");
    table.className = "file-list";
    table.innerHTML = `
        <thead>
            <tr>
                <th>File Name</th>
                <th>Type</th>
                <th>Created</th>
                <th>Expires</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            ${links.map(link => `
                <tr data-link-id="${link.id}">
                    <td>${link.linkType === "upload"
                        ? (link.description ? escapeHtml(link.description) : '<span class="muted">\u2014</span>')
                        : escapeHtml(link.fileName || "\u2014")}</td>
                    <td>${link.linkType === "upload"
                        ? '<span class="admin-user-role" style="background:rgba(165,242,31,0.1);color:var(--accent2)">Upload</span>'
                        : '<span class="admin-user-role" style="background:rgba(58,254,192,0.1);color:var(--accent)">Download</span>'}</td>
                    <td>${new Date(link.createdAt).toLocaleDateString()}</td>
                    <td>${new Date(link.expiresAt).toLocaleString()}</td>
                    <td>
                        <button class="copy-url-btn" data-url="${escapeHtml(link.shareUrl)}">Copy URL</button>
                        <button class="revoke-btn" data-link-id="${link.id}">Revoke</button>
                    </td>
                </tr>
            `).join("")}
        </tbody>
    `;

    container.innerHTML = "";
    container.appendChild(table);

    // Wire copy buttons
    container.querySelectorAll(".copy-url-btn").forEach(btn => {
        btn.addEventListener("click", () => {
            navigator.clipboard.writeText(btn.dataset.url).catch(() => {});
            btn.textContent = "Copied!";
            setTimeout(() => btn.textContent = "Copy URL", 2000);
        });
    });

    // Wire revoke buttons
    container.querySelectorAll(".revoke-btn").forEach(btn => {
        btn.addEventListener("click", async () => {
            if (!confirm("Revoke this link? It will stop working immediately.")) return;

            const linkId = btn.dataset.linkId;
            const resp = await fetchWithAuth(`${API_URL}/api/sharepoint/links/${linkId}`, { method: "DELETE" });
            if (!resp) return;
            if (resp.ok) {
                const row = document.querySelector(`tr[data-link-id="${linkId}"]`);
                if (row) row.remove();
                if (table.querySelector("tbody tr") === null) {
                    loadLinks(); // Reload to show empty state
                }
            } else {
                alert("Failed to revoke link.");
            }
        });
    });
}

// ─── Upload slot creation ─────────────────────────────────────────────────────

const newUploadSlotBtn = document.getElementById("newUploadSlotBtn");
const uploadSlotForm = document.getElementById("uploadSlotForm");
const cancelSlotBtn = document.getElementById("cancelSlotBtn");
const uploadSlotResult = document.getElementById("uploadSlotResult");

let selectedSlotExpiry = null;
let selectedSlotCount = null;

newUploadSlotBtn.addEventListener("click", () => {
    setVisible(uploadSlotForm, true);
    setVisible(uploadSlotResult, false);
});

cancelSlotBtn.addEventListener("click", () => {
    setVisible(uploadSlotForm, false);
});

// Expiry preset buttons
document.getElementById("uploadSlotExpiryPresets").querySelectorAll(".preset-btn").forEach(btn => {
    btn.addEventListener("click", () => {
        const hours = parseInt(btn.dataset.hours);
        selectedSlotExpiry = new Date(Date.now() + hours * 3600000);
        document.getElementById("uploadSlotExpiryPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
        btn.style.borderColor = "var(--accent)";
        const d = selectedSlotExpiry;
        const pad = n => String(n).padStart(2, '0');
        document.getElementById("uploadSlotCustomExpiry").value =
            `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    });
});

document.getElementById("uploadSlotCustomExpiry").addEventListener("change", e => {
    if (e.target.value) {
        selectedSlotExpiry = new Date(e.target.value);
        document.getElementById("uploadSlotExpiryPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
    }
});

// Count preset buttons
document.getElementById("uploadSlotCountPresets").querySelectorAll(".preset-btn").forEach(btn => {
    btn.addEventListener("click", () => {
        selectedSlotCount = parseInt(btn.dataset.count);
        document.getElementById("uploadSlotCountPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
        btn.style.borderColor = "var(--accent)";
        document.getElementById("uploadSlotCustomCount").value = btn.dataset.count;
    });
});

document.getElementById("uploadSlotCustomCount").addEventListener("input", e => {
    selectedSlotCount = parseInt(e.target.value);
    document.getElementById("uploadSlotCountPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
});

// Create slot submission
document.getElementById("createSlotBtn").addEventListener("click", async () => {
    const expiresAt = selectedSlotExpiry || (document.getElementById("uploadSlotCustomExpiry").value
        ? new Date(document.getElementById("uploadSlotCustomExpiry").value) : null);
    const maxFileCount = selectedSlotCount || parseInt(document.getElementById("uploadSlotCustomCount").value);
    const description = document.getElementById("uploadSlotDescription").value.trim() || null;

    if (!expiresAt || isNaN(maxFileCount) || maxFileCount < 1) {
        alert("Please select expiry and max file count.");
        return;
    }

    const createBtn = document.getElementById("createSlotBtn");
    createBtn.disabled = true;
    createBtn.style.opacity = "0.55";

    const resp = await fetchWithAuth(`${API_URL}/api/sharepoint/slots`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ expiresAt: expiresAt.toISOString(), description, maxFileCount })
    });

    createBtn.disabled = false;
    createBtn.style.opacity = "";

    if (!resp) return;
    if (!resp.ok) { alert("Failed to create upload slot."); return; }

    const data = await readJsonSafe(resp);
    setVisible(uploadSlotForm, false);
    setVisible(uploadSlotResult, true);
    document.getElementById("generatedSlotUrl").value = data.url;

    // Reset form state
    selectedSlotExpiry = null;
    selectedSlotCount = null;
    document.getElementById("uploadSlotExpiryPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
    document.getElementById("uploadSlotCountPresets").querySelectorAll(".preset-btn").forEach(b => b.style.borderColor = "");
    document.getElementById("uploadSlotDescription").value = "";
    document.getElementById("uploadSlotCustomExpiry").value = "";
    document.getElementById("uploadSlotCustomCount").value = "";

    // Reload links list to include new slot
    loadLinks();
});

// Copy slot URL button
document.getElementById("copySlotUrlBtn").addEventListener("click", () => {
    const url = document.getElementById("generatedSlotUrl").value;
    navigator.clipboard.writeText(url).catch(() => {});
    const btn = document.getElementById("copySlotUrlBtn");
    btn.textContent = "Copied!";
    setTimeout(() => btn.textContent = "Copy URL", 2000);
});

// ─── Bootstrap ───────────────────────────────────────────────────────────────

async function bootstrap() {
    const gate = document.getElementById("authLoadingGate");
    const app = document.getElementById("appContainer");

    // On page load, access token is empty (memory-only). Go straight to refresh.
    const hasRefresh = readPersistedRefreshToken();
    if (!hasRefresh) { window.location.href = "index.html"; return; }

    const ok = await refreshSession();
    if (!ok) { window.location.href = "index.html"; return; }

    // Fresh token — fetch user profile
    const meResponse = await fetch(`${API_URL}/auth/me`, { headers: authHeaders() });
    if (!meResponse.ok) { window.location.href = "index.html"; return; }

    const me = await readJsonSafe(meResponse);
    if (me?.isAdmin) {
        const adminLink = document.getElementById("adminLink");
        if (adminLink) adminLink.style.display = "";
    }

    gate.classList.add("view-hidden");
    app.classList.remove("view-hidden");
    app.classList.add("view-visible");

    loadLinks();
}

// ─── Wire events ─────────────────────────────────────────────────────────────

document.getElementById("logoutButton").addEventListener("click", logout);

bootstrap();

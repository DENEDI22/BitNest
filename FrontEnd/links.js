const API_URL = window.location.origin.replace("3000", "5000");
const REFRESH_LOCAL_KEY = "bitnest.refresh.local";
const REFRESH_SESSION_KEY = "bitnest.refresh.session";

const authState = {
    accessToken: "",
    refreshToken: "",
    rememberMe: false,
};

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

// ─── Links logic ─────────────────────────────────────────────────────────────

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}

async function loadLinks() {
    const container = document.getElementById("linksListContainer");
    container.innerHTML = '<p class="muted" style="padding:20px">Loading...</p>';

    const response = await fetch(`${API_URL}/api/sharepoint/links`, { headers: authHeaders() });

    if (response.status === 401) { window.location.href = "index.html"; return; }
    if (!response.ok) { container.innerHTML = '<p class="error" style="padding:20px">Failed to load links.</p>'; return; }

    const links = await readJsonSafe(response);

    if (!links || links.length === 0) {
        container.innerHTML = '<p class="empty-state">No active links — share a file from the Files view to create one.</p>';
        return;
    }

    const table = document.createElement("table");
    table.className = "file-list";
    table.innerHTML = `
        <thead>
            <tr>
                <th>File Name</th>
                <th>Created</th>
                <th>Expires</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            ${links.map(link => `
                <tr data-link-id="${link.id}">
                    <td>${escapeHtml(link.fileName)}</td>
                    <td>${new Date(link.createdAt).toLocaleDateString()}</td>
                    <td>${new Date(link.expiresAt).toLocaleString()}</td>
                    <td>
                        <button class="revoke-btn" data-link-id="${link.id}">Revoke</button>
                    </td>
                </tr>
            `).join("")}
        </tbody>
    `;

    container.innerHTML = "";
    container.appendChild(table);

    // Wire revoke buttons
    container.querySelectorAll(".revoke-btn").forEach(btn => {
        btn.addEventListener("click", async () => {
            if (!confirm("Revoke this link? It will stop working immediately.")) return;

            const linkId = btn.dataset.linkId;
            const resp = await fetch(`${API_URL}/api/sharepoint/links/${linkId}`, {
                method: "DELETE",
                headers: authHeaders()
            });

            if (resp.status === 401) { window.location.href = "index.html"; return; }
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

// ─── Bootstrap ───────────────────────────────────────────────────────────────

async function bootstrap() {
    const gate = document.getElementById("authLoadingGate");
    const app = document.getElementById("appContainer");

    let meResponse = await fetch(`${API_URL}/auth/me`, { headers: authHeaders() });

    if (!meResponse.ok) {
        const ok = await refreshSession();
        if (!ok) { window.location.href = "index.html"; return; }
        meResponse = await fetch(`${API_URL}/auth/me`, { headers: authHeaders() });
        if (!meResponse.ok) { window.location.href = "index.html"; return; }
    }

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

const API_URL = window.location.origin.replace("3000", "5000");
const REFRESH_LOCAL_KEY = "bitnest.refresh.local";
const REFRESH_SESSION_KEY = "bitnest.refresh.session";

const authState = {
    accessToken: "",
    refreshToken: "",
    rememberMe: false,
    isAdmin: false,
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

// ─── View helpers ────────────────────────────────────────────────────────────

function setVisible(el, visible) {
    if (!el) return;
    el.classList.toggle("view-hidden", !visible);
    el.classList.toggle("view-visible", visible);
}

// ─── Admin logic ─────────────────────────────────────────────────────────────

function showCreateUserForm() {
    setVisible(document.getElementById("createUserForm"), true);
}

function hideCreateUserForm() {
    setVisible(document.getElementById("createUserForm"), false);
    document.getElementById("createUsername").value = "";
    document.getElementById("createPassword").value = "";
    document.getElementById("createIsAdmin").checked = false;
}

async function loadAdminUsers() {
    const response = await fetchWithAuth(`${API_URL}/admin/users`);
    if (!response) return;
    if (!response.ok) return;
    renderAdminUserList(await response.json());
}

function renderAdminUserList(users) {
    const list = document.getElementById("adminUserList");
    list.innerHTML = "";
    if (!Array.isArray(users) || users.length === 0) {
        list.innerHTML = "<li class='admin-user-item'><p class='muted'>No users found.</p></li>";
        return;
    }
    users.forEach(user => {
        const li = document.createElement("li");
        li.className = "admin-user-item";

        const info = document.createElement("div");
        info.className = "admin-user-info";

        const name = document.createElement("span");
        name.className = "admin-user-name";
        name.textContent = user.username;

        const role = document.createElement("span");
        role.className = "admin-user-role";
        role.textContent = user.isAdmin ? "Admin" : "User";

        const status = document.createElement("span");
        status.className = `admin-user-status ${user.isActive ? "active" : "disabled"}`;
        status.textContent = user.isActive ? "Active" : "Disabled";

        info.appendChild(name);
        info.appendChild(role);
        info.appendChild(status);

        const actions = document.createElement("div");
        actions.className = "admin-user-actions";

        if (!user.isActive) {
            const note = document.createElement("span");
            note.className = "admin-action-note";
            note.textContent = "User disabled";
            actions.appendChild(note);
        } else {
            const btn = document.createElement("button");
            btn.className = "admin-action-button";
            btn.textContent = "Disable";
            btn.onclick = () => disableUser(user.id, user.username);
            actions.appendChild(btn);
        }

        li.appendChild(info);
        li.appendChild(actions);
        list.appendChild(li);
    });
}

async function submitCreateUser() {
    const username = document.getElementById("createUsername").value.trim();
    const password = document.getElementById("createPassword").value;
    const isAdmin = document.getElementById("createIsAdmin").checked;

    if (!username || !password) { alert("Username and password are required."); return; }
    if (password.length < 8) { alert("Password must be at least 8 characters."); return; }

    const response = await fetchWithAuth(`${API_URL}/admin/users`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password, isAdmin })
    });

    if (!response) return;
    if (response.status === 201 || response.ok) { hideCreateUserForm(); loadAdminUsers(); return; }

    const err = await readJsonSafe(response);
    alert(`Error creating user: ${err?.message || "Unknown error"}`);
}

async function disableUser(userId, username) {
    if (!confirm(`Disable user "${username}"? They will be signed out.`)) return;

    const response = await fetchWithAuth(`${API_URL}/admin/users/${userId}/disable`, { method: "POST" });
    if (!response) return;
    if (response.ok) { loadAdminUsers(); return; }

    const err = await readJsonSafe(response);
    alert(`Error disabling user: ${err?.message || "Unknown error"}`);
}

// ─── Bootstrap ───────────────────────────────────────────────────────────────

async function bootstrap() {
    const gate = document.getElementById("authLoadingGate");
    const app = document.getElementById("appContainer");
    const accessDenied = document.getElementById("accessDeniedView");
    const adminPanel = document.querySelector(".admin-card");

    // On page load, access token is empty (memory-only). Go straight to refresh.
    const hasRefresh = readPersistedRefreshToken();
    if (!hasRefresh) { window.location.href = "index.html"; return; }

    const ok = await refreshSession();
    if (!ok) { window.location.href = "index.html"; return; }

    // Fresh token — fetch user profile
    const meResponse = await fetch(`${API_URL}/auth/me`, { headers: authHeaders() });
    if (!meResponse.ok) { window.location.href = "index.html"; return; }

    const me = await readJsonSafe(meResponse);
    authState.isAdmin = me?.isAdmin === true;

    setVisible(gate, false);
    setVisible(app, true);

    if (!authState.isAdmin) {
        setVisible(adminPanel, false);
        setVisible(accessDenied, true);
        return;
    }

    loadAdminUsers();
}

// ─── Wire events ─────────────────────────────────────────────────────────────

document.getElementById("logoutButton").addEventListener("click", logout);
document.getElementById("showCreateUserBtn").addEventListener("click", showCreateUserForm);
document.getElementById("cancelCreateUserBtn").addEventListener("click", hideCreateUserForm);
document.getElementById("submitCreateUserBtn").addEventListener("click", submitCreateUser);

bootstrap();

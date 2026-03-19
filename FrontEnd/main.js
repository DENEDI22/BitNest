const files = new Map();
let currentPage = 1;
const API_URL = window.location.origin.replace("3000", "5000");
const REFRESH_LOCAL_KEY = "bitnest.refresh.local";
const REFRESH_SESSION_KEY = "bitnest.refresh.session";

const authState = {
    accessToken: "",
    refreshToken: "",
    rememberMe: false,
    resolved: false,
    isAdmin: false,
    userId: 0
};

const authContainer = document.getElementById("authContainer");
const appContainer = document.getElementById("appContainer");
const authLoadingGate = document.getElementById("authLoadingGate");
const authLoadingText = document.getElementById("authLoadingText");
const authUsernameInput = document.getElementById("authUsername");
const authPasswordInput = document.getElementById("authPassword");
const rememberMeInput = document.getElementById("rememberMe");
const authInlineMessage = document.getElementById("authInlineMessage");
const loginButton = document.getElementById("loginButton");
const signupButton = document.getElementById("signupButton");
const logoutButton = document.getElementById("logoutButton");
const fileInput = document.getElementById("fileInput");
const dropZone = document.getElementById("dropZone");
const filesView = document.getElementById("filesView");
const file404View = document.getElementById("file404View");
const accessDeniedView = document.getElementById("accessDeniedView");
const headerNav = document.getElementById("headerNav");
const adminLink = document.getElementById("adminLink");

const authActionButtons = [loginButton, signupButton].filter(Boolean);

// ─── View helpers ─────────────────────────────────────────────────────────────

function setViewVisible(element, visible) {
    if (!element) return;
    element.classList.toggle("view-hidden", !visible);
    element.classList.toggle("view-visible", visible);
}

function setAuthMessage(message, tone) {
    if (!authInlineMessage) return;
    if (!message) {
        authInlineMessage.textContent = "";
        authInlineMessage.classList.add("hidden");
        authInlineMessage.dataset.tone = "";
        return;
    }
    authInlineMessage.textContent = message;
    authInlineMessage.classList.remove("hidden");
    authInlineMessage.dataset.tone = tone || "error";
}

function showLoadingGate(message) {
    setViewVisible(authContainer, false);
    setViewVisible(appContainer, false);
    setViewVisible(authLoadingGate, true);
    if (authLoadingText) authLoadingText.textContent = message || "Checking your session...";
}

function hideLoadingGate() {
    setViewVisible(authLoadingGate, false);
}

function showAuthView(message, tone) {
    hideLoadingGate();
    setViewVisible(appContainer, false);
    setViewVisible(headerNav, false);
    setViewVisible(authContainer, true);
    setAuthMessage(message || "", tone);
    if (authUsernameInput) authUsernameInput.focus();
}

function showAppView() {
    hideLoadingGate();
    setViewVisible(authContainer, false);
    setViewVisible(appContainer, true);
    setViewVisible(headerNav, true);
    setAuthMessage("", "");
}

function showFilesView() {
    setViewVisible(filesView, true);
    setViewVisible(accessDeniedView, false);
    setViewVisible(file404View, false);
}

function showAccessDeniedView() {
    setViewVisible(filesView, false);
    setViewVisible(accessDeniedView, true);
    setViewVisible(file404View, false);
}

function show404View() {
    setViewVisible(filesView, false);
    setViewVisible(accessDeniedView, false);
    setViewVisible(file404View, true);
}

// ─── Auth helpers ─────────────────────────────────────────────────────────────

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

function setSession(tokens, rememberMe) {
    authState.accessToken = tokens.accessToken || "";
    authState.refreshToken = tokens.refreshToken || "";
    authState.rememberMe = Boolean(rememberMe);
    persistRefreshToken(authState.refreshToken, authState.rememberMe);
}

function resetAuthState() {
    authState.accessToken = "";
    authState.refreshToken = "";
    authState.rememberMe = false;
    clearPersistedRefreshToken();
}

function authHeaders(baseHeaders) {
    const headers = new Headers(baseHeaders || {});
    if (authState.accessToken) headers.set("Authorization", `Bearer ${authState.accessToken}`);
    return headers;
}

function normalizeErrorMessage(body, fallback) {
    if (body && typeof body.message === "string" && body.message.trim()) return body.message;
    return fallback;
}

async function readJsonSafe(response) {
    try { return await response.json(); } catch { return null; }
}

async function fetchCurrentUser() {
    return fetch(`${API_URL}/auth/me`, { method: "GET", headers: authHeaders() });
}

async function refreshSession(showFailureMessage) {
    const refreshToken = authState.refreshToken || readPersistedRefreshToken();
    if (!refreshToken) return false;

    const response = await fetch(`${API_URL}/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken })
    });

    if (!response.ok) {
        resetAuthState();
        if (showFailureMessage) showAuthView("Session expired. Please sign in again.", "error");
        return false;
    }

    const tokens = await readJsonSafe(response);
    if (!tokens || !tokens.accessToken || !tokens.refreshToken) {
        resetAuthState();
        if (showFailureMessage) showAuthView("Session expired. Please sign in again.", "error");
        return false;
    }

    setSession(tokens, authState.rememberMe);
    return true;
}

async function ensureAuthenticatedForAction() {
    const persistedRefreshToken = readPersistedRefreshToken();
    if (!persistedRefreshToken) {
        resetAuthState();
        showAuthView("Your session has ended. Please sign in again.", "error");
        return false;
    }

    if (!authState.accessToken) {
        const refreshed = await refreshSession(false);
        if (!refreshed) { showAuthView("Session expired. Please sign in again.", "error"); return false; }
    }

    const meResponse = await fetchCurrentUser();
    if (meResponse.ok) return true;

    const refreshed = await refreshSession(false);
    if (!refreshed) { showAuthView("Session expired. Please sign in again.", "error"); return false; }

    const retryResponse = await fetchCurrentUser();
    if (retryResponse.ok) return true;

    resetAuthState();
    showAuthView("Session expired. Please sign in again.", "error");
    return false;
}

function applyMeData(meData) {
    authState.isAdmin = meData.isAdmin === true;
    authState.userId = meData.id || 0;
    if (adminLink) adminLink.style.display = authState.isAdmin ? "" : "none";
}

async function bootstrapAuthGate() {
    showLoadingGate("Checking your session...");

    const meResponse = await fetchCurrentUser();
    if (meResponse.ok) {
        applyMeData(await meResponse.json());
        authState.resolved = true;
        showAppView();
        showFilesView();
        return true;
    }

    const refreshed = await refreshSession(false);
    if (refreshed) {
        const retryResponse = await fetchCurrentUser();
        if (retryResponse.ok) {
            applyMeData(await retryResponse.json());
            authState.resolved = true;
            showAppView();
            showFilesView();
            return true;
        }
    }

    authState.resolved = true;
    showAuthView("Please sign in to continue.", "info");
    return false;
}

// ─── Auth form ────────────────────────────────────────────────────────────────

function setAuthBusy(isBusy) {
    authActionButtons.forEach(btn => { btn.disabled = isBusy; });
}

function isValidAuthForm() {
    const username = authUsernameInput ? authUsernameInput.value.trim() : "";
    const password = authPasswordInput ? authPasswordInput.value : "";
    if (!username) { setAuthMessage("Username is required.", "error"); return false; }
    if (!password || password.length < 8) { setAuthMessage("Password must be at least 8 characters.", "error"); return false; }
    return true;
}

function loadCurrentPage() { loadFiles(currentPage); }

function enterAppState() {
    showAppView();
    showFilesView();
    currentPage = 1;
    loadCurrentPage();
}

async function performAuth(action) {
    if (!isValidAuthForm()) return;
    setAuthBusy(true);
    setAuthMessage("", "");

    const payload = {
        username: authUsernameInput.value.trim(),
        password: authPasswordInput.value,
        rememberMe: Boolean(rememberMeInput && rememberMeInput.checked)
    };

    const url = action === "signup" ? `${API_URL}/auth/signup` : `${API_URL}/auth/login`;
    const response = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        const body = await readJsonSafe(response);
        const fallback = action === "signup" ? "Could not sign up with those credentials." : "Invalid username or password.";
        setAuthMessage(normalizeErrorMessage(body, fallback), "error");
        setAuthBusy(false);
        return;
    }

    const tokens = await readJsonSafe(response);
    if (!tokens || !tokens.accessToken || !tokens.refreshToken) {
        setAuthMessage("Auth response was incomplete. Please try again.", "error");
        setAuthBusy(false);
        return;
    }

    setSession(tokens, payload.rememberMe);
    setAuthBusy(false);

    const meResponse = await fetchCurrentUser();
    if (meResponse.ok) applyMeData(await meResponse.json());

    enterAppState();
}

async function logout() {
    const refreshToken = authState.refreshToken || readPersistedRefreshToken();
    if (refreshToken) {
        await fetch(`${API_URL}/auth/logout`, {
            method: "POST",
            headers: { "Content-Type": "application/json", ...Object.fromEntries(authHeaders()) },
            body: JSON.stringify({ refreshToken })
        });
    }
    resetAuthState();
    showAuthView("Signed out.", "info");
    setTimeout(() => {
        if (authContainer && !authContainer.classList.contains("view-hidden")) setAuthMessage("", "");
    }, 1800);
}

// ─── Files ────────────────────────────────────────────────────────────────────

function formatFileSize(bytes) {
    if (bytes === 0) return "0 bytes";
    if (bytes < 1024) return `${bytes} bytes`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function splitFileName(fileName) {
    const lastDot = fileName.lastIndexOf(".");
    if (lastDot === -1 || lastDot === 0) return { name: fileName, extension: "" };
    return { name: fileName.substring(0, lastDot), extension: fileName.substring(lastDot) };
}

async function loadFiles(page) {
    const list = document.getElementById("fileList");
    if (!list) return;
    list.innerHTML = "";

    const response = await fetch(`${API_URL}/Storage/${page}`, { method: "GET", headers: authHeaders() });

    if (response.status === 401) { resetAuthState(); showAuthView("Session expired. Please sign in again.", "error"); return; }
    if (!response.ok) return;

    const data = await readJsonSafe(response);
    if (!Array.isArray(data)) return;

    data.forEach(f => {
        const li = document.createElement("li");
        const parts = splitFileName(f.FileName);

        const fileInfo = document.createElement("div");
        fileInfo.className = "file-info";

        const fileNameContainer = document.createElement("div");
        fileNameContainer.className = "file-name-container";

        const nameSpan = document.createElement("span");
        nameSpan.className = "file-name";
        nameSpan.textContent = parts.name;

        const extSpan = document.createElement("span");
        extSpan.className = "file-extension";
        extSpan.textContent = parts.extension;

        fileNameContainer.appendChild(nameSpan);
        fileNameContainer.appendChild(extSpan);

        const sizeSpan = document.createElement("span");
        sizeSpan.className = "file-size";
        sizeSpan.textContent = formatFileSize(f.Size);

        fileInfo.appendChild(fileNameContainer);
        fileInfo.appendChild(sizeSpan);

        const fileControls = document.createElement("div");
        fileControls.className = "file-controls";

        const downloadButton = document.createElement("button");
        downloadButton.textContent = "Download";
        files.set(f.Id, f);
        downloadButton.addEventListener("click", () => downloadFile(f.Id));

        const shareButton = document.createElement("button");
        shareButton.textContent = "Share";
        shareButton.className = "share-btn";
        shareButton.addEventListener("click", () => openShareModal(f.Id, f.FileName));

        const deleteButton = document.createElement("button");
        deleteButton.textContent = "Delete";
        deleteButton.addEventListener("click", () => deleteFile(f.Id));

        fileControls.appendChild(downloadButton);
        fileControls.appendChild(shareButton);
        fileControls.appendChild(deleteButton);

        li.appendChild(fileInfo);
        li.appendChild(fileControls);
        list.appendChild(li);
    });
}

async function deleteFile(fileId) {
    if (!(await ensureAuthenticatedForAction())) return;

    const response = await fetch(`${API_URL}/Storage/${fileId}`, { method: "DELETE", headers: authHeaders() });

    if (response.status === 401) { resetAuthState(); showAuthView("Session expired. Please sign in again.", "error"); return; }
    if (response.status === 404) { show404View(); return; }
    if (response.ok) { loadCurrentPage(); } else { show404View(); }
}

async function downloadFile(fileId) {
    if (!(await ensureAuthenticatedForAction())) return;

    const response = await fetch(`${API_URL}/Storage/download/${fileId}`, { method: "GET", headers: authHeaders() });

    if (response.status === 401) { resetAuthState(); showAuthView("Session expired. Please sign in again.", "error"); return; }
    if (response.status === 404 || !response.ok) { show404View(); return; }

    const blob = await response.blob();
    const file = files.get(fileId);
    const a = document.createElement("a");
    const url = window.URL.createObjectURL(blob);
    a.href = url;
    a.download = file && file.FileName ? file.FileName : `file-${fileId}`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
}

async function nextPage() {
    if (!(await ensureAuthenticatedForAction())) return;
    currentPage++;
    loadFiles(currentPage);
}

async function prevPage() {
    if (!(await ensureAuthenticatedForAction())) return;
    currentPage = Math.max(1, currentPage - 1);
    loadFiles(currentPage);
}

// ─── Upload ───────────────────────────────────────────────────────────────────

async function uploadFile() {
    if (!(await ensureAuthenticatedForAction())) return;

    const loader = document.getElementById("uploadIndicator");
    const progressContainer = document.getElementById("uploadProgress");
    const progressBar = document.getElementById("uploadProgressBar");
    const progressText = document.getElementById("uploadProgressText");

    if (!fileInput || !dropZone || dropZone.classList.contains("uploading")) return;

    const file = fileInput.files && fileInput.files[0];
    if (!file) return;

    const formData = new FormData();
    formData.append("formFile", file);

    dropZone.classList.add("uploading");
    if (loader) loader.style.display = "block";
    if (progressContainer) progressContainer.classList.remove("hidden");
    if (progressBar) progressBar.style.width = "0%";
    if (progressText) progressText.textContent = "Uploading...";

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${API_URL}/Storage`, true);
    if (authState.accessToken) xhr.setRequestHeader("Authorization", `Bearer ${authState.accessToken}`);

    xhr.upload.onprogress = event => {
        if (!event.lengthComputable) return;
        const percent = Math.round((event.loaded / event.total) * 100);
        if (progressBar) progressBar.style.width = `${percent}%`;
        if (progressText) progressText.textContent = `Uploading ${percent}%`;
    };

    function resetUploadState() {
        if (loader) loader.style.display = "none";
        if (dropZone) dropZone.classList.remove("uploading");
        if (fileInput) fileInput.value = "";
        if (progressContainer) progressContainer.classList.add("hidden");
        if (progressBar) progressBar.style.width = "0%";
        if (progressText) progressText.textContent = "";
    }

    xhr.onload = () => {
        resetUploadState();
        if (xhr.status === 401) { resetAuthState(); showAuthView("Session expired. Please sign in again.", "error"); return; }
        if (xhr.status >= 200 && xhr.status < 300) loadCurrentPage();
    };

    xhr.onerror = resetUploadState;
    xhr.send(formData);
}

function onFileSelected() { setTimeout(uploadFile, 0); }

function setupDropZone() {
    if (!dropZone) return;

    dropZone.addEventListener("dragover", e => { e.preventDefault(); e.stopPropagation(); dropZone.classList.add("drag-over"); });
    dropZone.addEventListener("dragleave", e => { e.preventDefault(); e.stopPropagation(); if (!dropZone.contains(e.relatedTarget)) dropZone.classList.remove("drag-over"); });
    dropZone.addEventListener("drop", e => {
        e.preventDefault(); e.stopPropagation();
        dropZone.classList.remove("drag-over");
        const droppedFile = e.dataTransfer && e.dataTransfer.files[0];
        if (!droppedFile || !fileInput) return;
        const transfer = new DataTransfer();
        transfer.items.add(droppedFile);
        fileInput.files = transfer.files;
        uploadFile();
    });
}

// ─── Share modal ──────────────────────────────────────────────────────────────

let currentShareFileId = null;

function openShareModal(fileId, fileName) {
    currentShareFileId = fileId;
    document.getElementById("shareFileName").textContent = fileName;
    document.getElementById("shareLinkModal").style.display = "flex";
    document.getElementById("generatedLinkContainer").style.display = "none";

    const defaultExpiry = new Date();
    defaultExpiry.setHours(defaultExpiry.getHours() + 24);
    document.getElementById("customExpiryInput").value = defaultExpiry.toISOString().slice(0, 16);
}

function setupShareModalEvents() {
    document.querySelectorAll(".preset-btn").forEach(btn => {
        btn.addEventListener("click", () => {
            const expiry = new Date();
            if (btn.dataset.hours) expiry.setHours(expiry.getHours() + parseInt(btn.dataset.hours));
            else if (btn.dataset.days) expiry.setDate(expiry.getDate() + parseInt(btn.dataset.days));
            document.getElementById("customExpiryInput").value = expiry.toISOString().slice(0, 16);
        });
    });

    document.getElementById("createLinkBtn").addEventListener("click", async () => {
        const expiresAt = new Date(document.getElementById("customExpiryInput").value).toISOString();

        try {
            const response = await fetch(`${API_URL}/api/sharepoint/links`, {
                method: "POST",
                headers: { "Content-Type": "application/json", ...Object.fromEntries(authHeaders()) },
                body: JSON.stringify({ fileId: currentShareFileId, expiresAt })
            });

            if (response.status === 401) {
                resetAuthState();
                showAuthView("Session expired. Please sign in again.", "error");
                document.getElementById("shareLinkModal").style.display = "none";
                return;
            }

            if (!response.ok) { alert("Failed to create link"); return; }

            const result = await response.json();
            document.getElementById("generatedLinkUrl").value = result.url;
            document.getElementById("generatedLinkContainer").style.display = "block";
        } catch (err) {
            alert("Error creating link");
            console.error(err);
        }
    });

    document.getElementById("copyGeneratedLinkBtn").addEventListener("click", () => {
        const input = document.getElementById("generatedLinkUrl");
        input.select();
        navigator.clipboard.writeText(input.value);
        const btn = document.getElementById("copyGeneratedLinkBtn");
        btn.textContent = "Copied!";
        setTimeout(() => btn.textContent = "Copy to Clipboard", 2000);
    });

    document.getElementById("cancelShareBtn").addEventListener("click", () => {
        document.getElementById("shareLinkModal").style.display = "none";
    });

    document.getElementById("shareLinkModal").addEventListener("click", e => {
        if (e.target.id === "shareLinkModal") document.getElementById("shareLinkModal").style.display = "none";
    });
}

// ─── Wire events ──────────────────────────────────────────────────────────────

function setupAuthEvents() {
    if (loginButton) loginButton.addEventListener("click", () => performAuth("login"));
    if (signupButton) signupButton.addEventListener("click", () => performAuth("signup"));
    if (authPasswordInput) {
        authPasswordInput.addEventListener("keydown", e => {
            if (e.key === "Enter") { e.preventDefault(); performAuth("login"); }
        });
    }
    if (logoutButton) logoutButton.addEventListener("click", logout);
}

function setupFileEvents() {
    if (fileInput) {
        fileInput.addEventListener("change", onFileSelected);
        fileInput.addEventListener("input", onFileSelected);
    }
    setupDropZone();
}

async function initializeApplication() {
    setupAuthEvents();
    setupFileEvents();
    setupShareModalEvents();
    await bootstrapAuthGate();
    if (authContainer && authContainer.classList.contains("view-hidden")) loadFiles(currentPage);
}

// ─── Globals for inline HTML onclick ─────────────────────────────────────────
window.nextPage = nextPage;
window.prevPage = prevPage;
window.showFilesView = showFilesView;
window.showAccessDeniedView = showAccessDeniedView;
window.show404View = show404View;

initializeApplication();

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
const adminView = document.getElementById("adminView");
const accessDeniedView = document.getElementById("accessDeniedView");
const file404View = document.getElementById("file404View");
const headerNav = document.getElementById("headerNav");
const adminLink = document.getElementById("adminLink");
const adminUserList = document.getElementById("adminUserList");
const createUserForm = document.getElementById("createUserForm");
const createUsername = document.getElementById("createUsername");
const createPassword = document.getElementById("createPassword");
const createIsAdmin = document.getElementById("createIsAdmin");

const authActionButtons = [loginButton, signupButton].filter(Boolean);

function setViewVisible(element, visible) {
    if (!element) {
        return;
    }

    element.classList.toggle("view-hidden", !visible);
    element.classList.toggle("view-visible", visible);
}

function setAuthMessage(message, tone) {
    if (!authInlineMessage) {
        return;
    }

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
    if (authLoadingText) {
        authLoadingText.textContent = message || "Checking your session...";
    }
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
    if (authUsernameInput) {
        authUsernameInput.focus();
    }
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
    setViewVisible(adminView, false);
    setViewVisible(accessDeniedView, false);
    setViewVisible(file404View, false);
}

function showAdminView() {
    if (!authState.isAdmin) {
        showAccessDeniedView();
        return;
    }
    setViewVisible(filesView, false);
    setViewVisible(adminView, true);
    setViewVisible(accessDeniedView, false);
    setViewVisible(file404View, false);
    loadAdminUsers();
}

function showAccessDeniedView() {
    setViewVisible(filesView, false);
    setViewVisible(adminView, false);
    setViewVisible(accessDeniedView, true);
    setViewVisible(file404View, false);
}

function show404View() {
    setViewVisible(filesView, false);
    setViewVisible(adminView, false);
    setViewVisible(accessDeniedView, false);
    setViewVisible(file404View, true);
}

function showCreateUserForm() {
    setViewVisible(createUserForm, true);
}

function hideCreateUserForm() {
    setViewVisible(createUserForm, false);
    if (createUsername) createUsername.value = "";
    if (createPassword) createPassword.value = "";
    if (createIsAdmin) createIsAdmin.checked = false;
}

function persistRefreshToken(token, rememberMe) {
    try {
        window.localStorage.removeItem(REFRESH_LOCAL_KEY);
        window.sessionStorage.removeItem(REFRESH_SESSION_KEY);

        if (!token) {
            return;
        }

        if (rememberMe) {
            window.localStorage.setItem(REFRESH_LOCAL_KEY, token);
            return;
        }

        window.sessionStorage.setItem(REFRESH_SESSION_KEY, token);
    } catch {
        // Ignore storage access issues and keep in-memory state only.
    }
}

function readPersistedRefreshToken() {
    try {
        const rememberedToken = window.localStorage.getItem(REFRESH_LOCAL_KEY);
        if (rememberedToken) {
            authState.refreshToken = rememberedToken;
            authState.rememberMe = true;
            return rememberedToken;
        }

        const sessionToken = window.sessionStorage.getItem(REFRESH_SESSION_KEY);
        if (sessionToken) {
            authState.refreshToken = sessionToken;
            authState.rememberMe = false;
            return sessionToken;
        }
    } catch {
        return authState.refreshToken;
    }

    return "";
}

function clearPersistedRefreshToken() {
    try {
        window.localStorage.removeItem(REFRESH_LOCAL_KEY);
        window.sessionStorage.removeItem(REFRESH_SESSION_KEY);
    } catch {
        // Ignore storage access issues and keep in-memory state only.
    }
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
    if (authState.accessToken) {
        headers.set("Authorization", `Bearer ${authState.accessToken}`);
    }

    return headers;
}

function normalizeErrorMessage(body, fallback) {
    if (body && typeof body.message === "string" && body.message.trim()) {
        return body.message;
    }

    return fallback;
}

async function readJsonSafe(response) {
    try {
        return await response.json();
    } catch {
        return null;
    }
}

async function fetchCurrentUser() {
    const response = await fetch(`${API_URL}/auth/me`, {
        method: "GET",
        headers: authHeaders()
    });

    return response;
}

async function refreshSession(showFailureMessage) {
    const refreshToken = authState.refreshToken || readPersistedRefreshToken();
    if (!refreshToken) {
        return false;
    }

    const response = await fetch(`${API_URL}/auth/refresh`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ refreshToken })
    });

    if (!response.ok) {
        resetAuthState();
        if (showFailureMessage) {
            showAuthView("Session expired. Please sign in again.", "error");
        }

        return false;
    }

    const tokens = await readJsonSafe(response);
    if (!tokens || !tokens.accessToken || !tokens.refreshToken) {
        resetAuthState();
        if (showFailureMessage) {
            showAuthView("Session expired. Please sign in again.", "error");
        }

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
        const refreshedWithoutAccess = await refreshSession(false);
        if (!refreshedWithoutAccess) {
            showAuthView("Session expired. Please sign in again.", "error");
            return false;
        }
    }

    const meResponse = await fetchCurrentUser();
    if (meResponse.ok) {
        return true;
    }

    const refreshed = await refreshSession(false);
    if (!refreshed) {
        showAuthView("Session expired. Please sign in again.", "error");
        return false;
    }

    const retryResponse = await fetchCurrentUser();
    if (retryResponse.ok) {
        return true;
    }

    resetAuthState();
    showAuthView("Session expired. Please sign in again.", "error");
    return false;
}

async function bootstrapAuthGate() {
    showLoadingGate("Checking your session...");

    const meResponse = await fetchCurrentUser();
    if (meResponse.ok) {
        const meData = await meResponse.json();
        authState.isAdmin = meData.isAdmin === true;
        authState.userId = meData.id || 0;
        
        // Update admin link visibility
        if (adminLink) {
            adminLink.style.display = authState.isAdmin ? "block" : "none";
        }
        
        // Handle direct /admin route access
        if (window.location.pathname === "/admin") {
            if (authState.isAdmin) {
                authState.resolved = true;
                showAppView();
                showAdminView();
            } else {
                authState.resolved = true;
                showAppView();
                showAccessDeniedView();
            }
        } else {
            authState.resolved = true;
            showAppView();
            showFilesView();
        }
        return true;
    }

    const refreshed = await refreshSession(false);
    if (refreshed) {
        const retryResponse = await fetchCurrentUser();
        if (retryResponse.ok) {
            const meData = await retryResponse.json();
            authState.isAdmin = meData.isAdmin === true;
            authState.userId = meData.id || 0;
            
            // Update admin link visibility
            if (adminLink) {
                adminLink.style.display = authState.isAdmin ? "block" : "none";
            }
            
            // Handle direct /admin route access
            if (window.location.pathname === "/admin") {
                if (authState.isAdmin) {
                    authState.resolved = true;
                    showAppView();
                    showAdminView();
                } else {
                    authState.resolved = true;
                    showAppView();
                    showAccessDeniedView();
                }
            } else {
                authState.resolved = true;
                showAppView();
                showFilesView();
            }
            return true;
        }
    }

    authState.resolved = true;
    showAuthView("Please sign in to continue.", "info");
    return false;
}

function setAuthBusy(isBusy) {
    authActionButtons.forEach((button) => {
        button.disabled = isBusy;
    });
}

function isValidAuthForm() {
    const username = authUsernameInput ? authUsernameInput.value.trim() : "";
    const password = authPasswordInput ? authPasswordInput.value : "";

    if (!username) {
        setAuthMessage("Username is required.", "error");
        return false;
    }

    if (!password || password.length < 8) {
        setAuthMessage("Password must be at least 8 characters.", "error");
        return false;
    }

    return true;
}

function loadCurrentPage() {
    const pageToLoad = currentPage;
    loadFiles(pageToLoad);
}

function enterAppState() {
    showAppView();
    currentPage = 1;
    loadCurrentPage();
}

async function performAuth(action) {
    if (!isValidAuthForm()) {
        return;
    }

    setAuthBusy(true);
    setAuthMessage("", "");

    const payload = {
        username: authUsernameInput.value.trim(),
        password: authPasswordInput.value,
        rememberMe: Boolean(rememberMeInput && rememberMeInput.checked)
    };

    const response = action === "signup"
        ? await fetch(`${API_URL}/auth/signup`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        })
        : await fetch(`${API_URL}/auth/login`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        });

    if (!response.ok) {
        const body = await readJsonSafe(response);
        const fallback = action === "signup"
            ? "Could not sign up with those credentials."
            : "Invalid username or password.";
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
    enterAppState();
}

async function logout() {
    const refreshToken = authState.refreshToken || readPersistedRefreshToken();

    if (refreshToken) {
        await fetch(`${API_URL}/auth/logout`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                ...Object.fromEntries(authHeaders())
            },
            body: JSON.stringify({ refreshToken })
        });
    }

    resetAuthState();
    showAuthView("Signed out.", "info");
    setTimeout(() => {
        if (authContainer && !authContainer.classList.contains("view-hidden")) {
            setAuthMessage("", "");
        }
    }, 1800);
}

async function uploadFile() {
    if (!(await ensureAuthenticatedForAction())) {
        return;
    }

    const loader = document.getElementById("uploadIndicator");
    const progressContainer = document.getElementById("uploadProgress");
    const progressBar = document.getElementById("uploadProgressBar");
    const progressText = document.getElementById("uploadProgressText");

    if (!fileInput || !dropZone || dropZone.classList.contains("uploading")) {
        return;
    }

    const file = fileInput.files && fileInput.files[0];
    if (!file) {
        return;
    }

    const formData = new FormData();
    formData.append("formFile", file);

    dropZone.classList.add("uploading");
    if (loader) {
        loader.style.display = "block";
    }

    if (progressContainer) {
        progressContainer.classList.remove("hidden");
    }

    if (progressBar) {
        progressBar.style.width = "0%";
    }

    if (progressText) {
        progressText.textContent = "Uploading...";
    }

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${API_URL}/Storage`, true);
    if (authState.accessToken) {
        xhr.setRequestHeader("Authorization", `Bearer ${authState.accessToken}`);
    }

    xhr.upload.onprogress = function (event) {
        if (!event.lengthComputable) {
            return;
        }

        const percent = Math.round((event.loaded / event.total) * 100);
        if (progressBar) {
            progressBar.style.width = `${percent}%`;
        }

        if (progressText) {
            progressText.textContent = `Uploading ${percent}%`;
        }

        if (loader) {
            loader.setAttribute("aria-label", `Uploading: ${percent}%`);
        }
    };

    function resetUploadState() {
        if (loader) {
            loader.style.display = "none";
        }

        if (dropZone) {
            dropZone.classList.remove("uploading");
        }

        if (fileInput) {
            fileInput.value = "";
        }

        if (progressContainer) {
            progressContainer.classList.add("hidden");
        }

        if (progressBar) {
            progressBar.style.width = "0%";
        }

        if (progressText) {
            progressText.textContent = "";
        }
    }

    xhr.onload = function () {
        resetUploadState();

        if (xhr.status === 401) {
            resetAuthState();
            showAuthView("Session expired. Please sign in again.", "error");
            return;
        }

        if (xhr.status >= 200 && xhr.status < 300) {
            loadCurrentPage();
        }
    };

    xhr.onerror = function () {
        resetUploadState();
    };

    xhr.send(formData);
}

async function nextPage() {
    if (!(await ensureAuthenticatedForAction())) {
        return;
    }

    currentPage = currentPage + 1;
    const pageToLoad = currentPage;
    loadFiles(pageToLoad);
}

async function prevPage() {
    if (!(await ensureAuthenticatedForAction())) {
        return;
    }

    currentPage = Math.max(1, currentPage - 1);
    const pageToLoad = currentPage;
    loadFiles(pageToLoad);
}

function formatFileSize(bytes) {
    if (bytes === 0) return "0 bytes";
    if (bytes < 1024) return `${bytes} bytes`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function splitFileName(fileName) {
    const lastDotIndex = fileName.lastIndexOf(".");
    if (lastDotIndex === -1 || lastDotIndex === 0) {
        return { name: fileName, extension: "" };
    }

    return {
        name: fileName.substring(0, lastDotIndex),
        extension: fileName.substring(lastDotIndex)
    };
}

async function loadFiles(page) {
    const list = document.getElementById("fileList");
    if (!list) {
        return;
    }

    list.innerHTML = "";

    const response = await fetch(`${API_URL}/Storage/${page}`, {
        method: "GET",
        headers: authHeaders()
    });

    if (response.status === 401) {
        resetAuthState();
        showAuthView("Session expired. Please sign in again.", "error");
        return;
    }

    if (!response.ok) {
        return;
    }

    const data = await readJsonSafe(response);
    if (!Array.isArray(data)) {
        return;
    }

    data.forEach((f) => {
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

        const deleteButton = document.createElement("button");
        deleteButton.textContent = "Delete";
        deleteButton.addEventListener("click", () => deleteFile(f.Id));

        fileControls.appendChild(downloadButton);
        fileControls.appendChild(deleteButton);

        li.appendChild(fileInfo);
        li.appendChild(fileControls);
        list.appendChild(li);
    });
}

async function deleteFile(fileId) {
    if (!(await ensureAuthenticatedForAction())) {
        return;
    }

    const response = await fetch(`${API_URL}/Storage/${fileId}`, {
        method: "DELETE",
        headers: authHeaders()
    });

    if (response.status === 401) {
        resetAuthState();
        showAuthView("Session expired. Please sign in again.", "error");
        return;
    }

    if (response.status === 404) {
        show404View();
        return;
    }

    if (response.ok) {
        loadCurrentPage();
    } else {
        show404View();
    }
}

async function downloadFile(fileId) {
    if (!(await ensureAuthenticatedForAction())) {
        return;
    }

    const response = await fetch(`${API_URL}/Storage/download/${fileId}`, {
        method: "GET",
        headers: authHeaders()
    });

    if (response.status === 401) {
        resetAuthState();
        showAuthView("Session expired. Please sign in again.", "error");
        return;
    }

    if (response.status === 404) {
        show404View();
        return;
    }

    if (!response.ok) {
        show404View();
        return;
    }

    const blob = await response.blob();
    const file = files.get(fileId);
    const downloadLink = document.createElement("a");
    const url = window.URL.createObjectURL(blob);
    downloadLink.href = url;
    downloadLink.download = file && file.FileName ? file.FileName : `file-${fileId}`;
    document.body.appendChild(downloadLink);
    downloadLink.click();
    downloadLink.remove();
    window.URL.revokeObjectURL(url);
}

function onFileSelected() {
    setTimeout(uploadFile, 0);
}

function setupDropZone() {
    if (!dropZone) {
        return;
    }

    dropZone.addEventListener("dragover", (event) => {
        event.preventDefault();
        event.stopPropagation();
        dropZone.classList.add("drag-over");
    });

    dropZone.addEventListener("dragleave", (event) => {
        event.preventDefault();
        event.stopPropagation();
        if (!dropZone.contains(event.relatedTarget)) {
            dropZone.classList.remove("drag-over");
        }
    });

    dropZone.addEventListener("drop", (event) => {
        event.preventDefault();
        event.stopPropagation();
        dropZone.classList.remove("drag-over");

        const droppedFile = event.dataTransfer && event.dataTransfer.files[0];
        if (!droppedFile || !fileInput) {
            return;
        }

        const transfer = new DataTransfer();
        transfer.items.add(droppedFile);
        fileInput.files = transfer.files;
        uploadFile();
    });
}

function setupAuthEvents() {
    if (loginButton) {
        loginButton.addEventListener("click", () => performAuth("login"));
    }

    if (signupButton) {
        signupButton.addEventListener("click", () => performAuth("signup"));
    }

    if (authPasswordInput) {
        authPasswordInput.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                performAuth("login");
            }
        });
    }

    if (logoutButton) {
        logoutButton.addEventListener("click", logout);
    }
}

function setupFileEvents() {
    if (fileInput) {
        fileInput.addEventListener("change", onFileSelected);
        fileInput.addEventListener("input", onFileSelected);
    }

    setupDropZone();
}

async function loadAdminUsers() {
    if (!authState.isAdmin) {
        return;
    }

    try {
        const response = await fetch(`${API_URL}/admin/users`, {
            method: "GET",
            headers: authHeaders()
        });

        if (response.status === 401) {
            resetAuthState();
            showAuthView("Session expired. Please sign in again.", "error");
            return;
        }

        if (!response.ok) {
            console.error("Failed to load admin users:", response.status);
            return;
        }

        const users = await response.json();
        renderAdminUserList(users);
    } catch (error) {
        console.error("Error loading admin users:", error);
    }
}

function renderAdminUserList(users) {
    if (!adminUserList) {
        return;
    }

    adminUserList.innerHTML = "";

    if (!Array.isArray(users) || users.length === 0) {
        adminUserList.innerHTML = "<li class='admin-user-item'><p class='muted'>No users found.</p></li>";
        return;
    }

    users.forEach((user) => {
        const li = document.createElement("li");
        li.className = "admin-user-item";

        const userInfo = document.createElement("div");
        userInfo.className = "admin-user-info";

        const name = document.createElement("span");
        name.className = "admin-user-name";
        name.textContent = user.username;

        const role = document.createElement("span");
        role.className = "admin-user-role";
        role.textContent = user.isAdmin ? "Admin" : "User";

        const status = document.createElement("span");
        status.className = `admin-user-status ${user.isActive ? "active" : "disabled"}`;
        status.textContent = user.isActive ? "Active" : "Disabled";

        userInfo.appendChild(name);
        userInfo.appendChild(role);
        userInfo.appendChild(status);

        const actions = document.createElement("div");
        actions.className = "admin-user-actions";

        if (!user.isActive) {
            const cannotDisable = document.createElement("span");
            cannotDisable.className = "admin-action-note";
            cannotDisable.textContent = "User disabled";
            actions.appendChild(cannotDisable);
        } else {
            const disableButton = document.createElement("button");
            disableButton.className = "admin-action-button";
            disableButton.textContent = "Disable";
            disableButton.onclick = () => disableUser(user.id, user.username);
            actions.appendChild(disableButton);
        }

        li.appendChild(userInfo);
        li.appendChild(actions);
        adminUserList.appendChild(li);
    });
}

async function submitCreateUser() {
    if (!authState.isAdmin) {
        return;
    }

    const username = createUsername ? createUsername.value.trim() : "";
    const password = createPassword ? createPassword.value : "";
    const isAdmin = createIsAdmin ? createIsAdmin.checked : false;

    if (!username || !password) {
        alert("Username and password are required.");
        return;
    }

    if (password.length < 8) {
        alert("Password must be at least 8 characters.");
        return;
    }

    try {
        const response = await fetch(`${API_URL}/admin/users`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                ...authHeaders()
            },
            body: JSON.stringify({
                username,
                password,
                isAdmin
            })
        });

        if (response.status === 401) {
            resetAuthState();
            showAuthView("Session expired. Please sign in again.", "error");
            return;
        }

        if (response.status === 201 || response.ok) {
            hideCreateUserForm();
            loadAdminUsers();
            return;
        }

        const errorBody = await readJsonSafe(response);
        alert(`Error creating user: ${errorBody.message || "Unknown error"}`);
    } catch (error) {
        console.error("Error creating user:", error);
        alert("Failed to create user.");
    }
}

async function disableUser(userId, username) {
    if (!authState.isAdmin) {
        return;
    }

    if (!confirm(`Disable user "${username}"? They will be signed out.`)) {
        return;
    }

    try {
        const response = await fetch(`${API_URL}/admin/users/${userId}/disable`, {
            method: "POST",
            headers: authHeaders()
        });

        if (response.status === 401) {
            resetAuthState();
            showAuthView("Session expired. Please sign in again.", "error");
            return;
        }

        if (response.ok) {
            loadAdminUsers();
            return;
        }

        const errorBody = await readJsonSafe(response);
        alert(`Error disabling user: ${errorBody.message || "Unknown error"}`);
    } catch (error) {
        console.error("Error disabling user:", error);
        alert("Failed to disable user.");
    }
}

function setupAdminEvents() {
    if (adminLink) {
        adminLink.addEventListener("click", showAdminView);
    }
}

async function initializeApplication() {
    setupAuthEvents();
    setupAdminEvents();
    setupFileEvents();
    await bootstrapAuthGate();
    if (authContainer && authContainer.classList.contains("view-hidden")) {
        loadFiles(currentPage);
    }
}

window.nextPage = nextPage;
window.prevPage = prevPage;
window.showFilesView = showFilesView;
window.showAdminView = showAdminView;
window.showAccessDeniedView = showAccessDeniedView;
window.show404View = show404View;
window.showCreateUserForm = showCreateUserForm;
window.hideCreateUserForm = hideCreateUserForm;
window.submitCreateUser = submitCreateUser;
window.disableUser = disableUser;

initializeApplication();

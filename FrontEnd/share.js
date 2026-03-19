const token = new URLSearchParams(window.location.search).get('token');
const API_URL = window.location.origin.replace("3000", "5000");

const loadingState = document.getElementById('loadingState');
const downloadView = document.getElementById('downloadView');
const expiredView = document.getElementById('expiredView');
const downloadError = document.getElementById('downloadError');

function setVisible(el, visible) {
    if (!el) return;
    el.classList.toggle('view-hidden', !visible);
    el.classList.toggle('view-visible', visible);
}

function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
}

function showExpiredView() {
    setVisible(loadingState, false);
    setVisible(downloadView, false);
    setVisible(expiredView, true);
}

async function loadFileMetadata() {
    try {
        const response = await fetch(`${API_URL}/api/share/${token}`);
        if (!response.ok) { showExpiredView(); return; }

        const data = await response.json();

        document.getElementById('fileName').textContent = data.fileName;
        document.getElementById('fileSize').textContent = formatFileSize(data.fileSize);
        document.getElementById('expiresAt').textContent = new Date(data.expiresAt).toLocaleString();

        setVisible(loadingState, false);
        setVisible(downloadView, true);
    } catch (err) {
        console.error('Error loading metadata:', err);
        showExpiredView();
    }
}

function triggerDownload() {
    // Navigate directly — browser streams and shows native download progress.
    // Public endpoint requires no auth header; token is in the path.
    window.location.href = `${API_URL}/api/share/${encodeURIComponent(token)}/download`;
}

document.getElementById('downloadBtn').addEventListener('click', triggerDownload);

if (!token) {
    showExpiredView();
} else {
    loadFileMetadata();
}

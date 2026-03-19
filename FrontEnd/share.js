// Extract token from URL path (/share/{token})
const pathParts = window.location.pathname.split('/');
const token = pathParts[pathParts.length - 1];

const API_URL = window.location.origin.replace("3000", "5000");

const loadingState = document.getElementById('loadingState');
const downloadView = document.getElementById('downloadView');
const expiredView = document.getElementById('expiredView');
const downloadError = document.getElementById('downloadError');

async function loadFileMetadata() {
    try {
        const response = await fetch(`${API_URL}/api/share/${token}`);
        
        if (!response.ok) {
            // Expired, revoked, or invalid token
            showExpiredView();
            return;
        }
        
        const data = await response.json();
        
        // Populate file info
        document.getElementById('fileName').textContent = data.fileName;
        document.getElementById('fileSize').textContent = formatFileSize(data.fileSize);
        document.getElementById('expiresAt').textContent = new Date(data.expiresAt).toLocaleString();
        
        // Show download view
        loadingState.style.display = 'none';
        downloadView.style.display = 'block';
        
    } catch (error) {
        console.error('Error loading metadata:', error);
        showExpiredView();
    }
}

function showExpiredView() {
    loadingState.style.display = 'none';
    downloadView.style.display = 'none';
    expiredView.style.display = 'block';
}

async function triggerDownload() {
    try {
        const response = await fetch(`${API_URL}/api/share/${token}/download`);
        
        if (!response.ok) {
            downloadError.style.display = 'block';
            return;
        }
        
        // Trigger download
        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = document.getElementById('fileName').textContent;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
        
    } catch (error) {
        console.error('Download error:', error);
        downloadError.style.display = 'block';
    }
}

function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
}

// Wire download button
document.getElementById('downloadBtn').addEventListener('click', triggerDownload);

// Wire retry button
document.getElementById('retryBtn').addEventListener('click', () => {
    downloadError.style.display = 'none';
    triggerDownload();
});

// Load on page load
loadFileMetadata();

const token = new URLSearchParams(window.location.search).get('token');
const API_URL = window.location.origin.replace("3000", "5000");

const loadingState = document.getElementById('loadingState');
const uploadView = document.getElementById('uploadView');
const expiredView = document.getElementById('expiredView');
const slotFullView = document.getElementById('slotFullView');
const dropzone = document.getElementById('dropzone');
const fileInput = document.getElementById('fileInput');
const progressContainer = document.getElementById('progressContainer');
const progressBar = document.getElementById('progressBar');
const progressText = document.getElementById('progressText');
const successMessage = document.getElementById('successMessage');
const errorMessage = document.getElementById('errorMessage');

function setVisible(el, visible) {
    if (!el) return;
    el.classList.toggle('view-hidden', !visible);
    el.classList.toggle('view-visible', visible);
}

function showExpiredView() {
    setVisible(loadingState, false);
    setVisible(uploadView, false);
    setVisible(expiredView, true);
    setVisible(slotFullView, false);
}

function showSlotFullView() {
    setVisible(loadingState, false);
    setVisible(uploadView, false);
    setVisible(expiredView, false);
    setVisible(slotFullView, true);
}

function uploadFile(file) {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${API_URL}/api/share/${encodeURIComponent(token)}/upload`, true);
    // NO Authorization header — token in URL is the credential
    const formData = new FormData();
    formData.append("formFile", file);

    setVisible(progressContainer, true);
    setVisible(successMessage, false);
    setVisible(errorMessage, false);
    dropzone.classList.add('uploading');
    progressBar.style.width = '0%';
    progressText.textContent = 'Uploading 0%';

    xhr.upload.onprogress = event => {
        if (!event.lengthComputable) return;
        const percent = Math.round((event.loaded / event.total) * 100);
        progressBar.style.width = `${percent}%`;
        progressText.textContent = `Uploading ${percent}%`;
    };

    xhr.onload = () => {
        dropzone.classList.remove('uploading');
        setVisible(progressContainer, false);

        if (xhr.status >= 200 && xhr.status < 300) {
            // Success — show inline message, reset form after 2s
            setVisible(successMessage, true);
            fileInput.value = '';
            // Update remaining count if displayed
            const remainingEl = document.getElementById('remainingCount');
            if (remainingEl && remainingEl.textContent) {
                const current = parseInt(remainingEl.textContent);
                if (!isNaN(current) && current > 0) {
                    const newRemaining = current - 1;
                    remainingEl.textContent = `${newRemaining} uploads remaining`;
                    if (newRemaining <= 0) {
                        // Slot is now full — transition to slot full view
                        setVisible(uploadView, false);
                        setVisible(slotFullView, true);
                        return;
                    }
                }
            }
            setTimeout(() => setVisible(successMessage, false), 2000);
        } else if (xhr.status === 409) {
            // Slot became full mid-upload — transition to slot full view
            setVisible(uploadView, false);
            setVisible(slotFullView, true);
        } else {
            setVisible(errorMessage, true);
        }
    };

    xhr.onerror = () => {
        dropzone.classList.remove('uploading');
        setVisible(progressContainer, false);
        setVisible(errorMessage, true);
    };

    xhr.send(formData);
}

async function loadMetadata() {
    try {
        const response = await fetch(`${API_URL}/api/share/${encodeURIComponent(token)}`);
        if (!response.ok) { showExpiredView(); return; }

        const data = await response.json();

        if (data.linkType !== "upload") { showExpiredView(); return; }

        if (data.maxFileCount !== null && data.uploadCount >= data.maxFileCount) {
            showSlotFullView();
            return;
        }

        // Populate context card
        document.getElementById('slotHeading').textContent = data.description || 'Upload Files';
        document.getElementById('ownerUsername').textContent = data.ownerUsername;
        document.getElementById('createdAt').textContent = new Date(data.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
        document.getElementById('expiresAt').textContent = new Date(data.expiresAt).toLocaleString();

        if (data.description) {
            document.getElementById('slotDescription').textContent = data.description;
            setVisible(document.getElementById('metaDescription'), true);
        }

        if (data.maxFileCount !== null) {
            const remaining = data.maxFileCount - data.uploadCount;
            document.getElementById('remainingCount').textContent = `${remaining} uploads remaining`;
            setVisible(document.getElementById('metaRemaining'), true);
        }

        setVisible(loadingState, false);
        setVisible(uploadView, true);
    } catch (err) {
        console.error('Error loading metadata:', err);
        showExpiredView();
    }
}

// ─── Dropzone interactions ────────────────────────────────────────────────────

dropzone.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', () => {
    if (fileInput.files && fileInput.files[0]) {
        uploadFile(fileInput.files[0]);
    }
});

dropzone.addEventListener('dragenter', e => { e.preventDefault(); e.stopPropagation(); dropzone.classList.add('drag-over'); });
dropzone.addEventListener('dragover', e => { e.preventDefault(); e.stopPropagation(); dropzone.classList.add('drag-over'); });
dropzone.addEventListener('dragleave', e => { e.preventDefault(); e.stopPropagation(); if (!dropzone.contains(e.relatedTarget)) dropzone.classList.remove('drag-over'); });
dropzone.addEventListener('drop', e => {
    e.preventDefault();
    e.stopPropagation();
    dropzone.classList.remove('drag-over');
    const file = e.dataTransfer && e.dataTransfer.files[0];
    if (file) uploadFile(file);
});

// ─── Bootstrap ───────────────────────────────────────────────────────────────

if (!token) {
    setVisible(loadingState, false);
    setVisible(expiredView, true);
} else {
    loadMetadata();
}

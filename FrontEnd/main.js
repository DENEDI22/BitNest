const files = new Map();
let currentPage = 1;
const API_URL = window.location.origin.replace("3000", "5000");

function uploadFile() {
    const fileInput = document.getElementById("fileInput");
    const loader = document.getElementById("uploadIndicator");
    const uploadButton = document.getElementById("uploadbtn");
    const progressContainer = document.getElementById("uploadProgress");
    const progressBar = document.getElementById("uploadProgressBar");
    const progressText = document.getElementById("uploadProgressText");

    const file = fileInput.files[0];
    if (!file) return;

    const formData = new FormData();
    formData.append("formFile", file);

    uploadButton.disabled = true;
    loader.style.display = "block";
    progressContainer.classList.remove("hidden");
    progressBar.style.width = "0%";
    progressText.textContent = "Uploading…";

    const xhr = new XMLHttpRequest();

    xhr.open("POST", `${API_URL}/Storage`, true);

    xhr.upload.onprogress = function (event) {
        if (!event.lengthComputable) return;

        const percent = Math.round((event.loaded / event.total) * 100);
        progressBar.style.width = `${percent}%`;
        progressText.textContent = `Uploading ${percent}%`;
        loader.setAttribute("aria-label", `Uploading: ${percent}%`);
    };

    function resetState() {
        loader.style.display = "none";
        uploadButton.disabled = false;
        progressContainer.classList.add("hidden");
        progressBar.style.width = "0%";
        progressText.textContent = "";
    }

    xhr.onload = function () {
        resetState();
        if (xhr.status >= 200 && xhr.status < 300) {
            loadFiles();
        }
    };

    xhr.onerror = function () {
        resetState();
    };

    xhr.send(formData);
}

async function nextPage() {
    currentPage = currentPage + 1;
    loadFiles(currentPage);
}

async function prevPage() {
    currentPage = currentPage - 1;
    loadFiles(currentPage);
}

async function loadFiles(page) {
    const list = document.getElementById("fileList");
    list.innerHTML = "";

    const res = await fetch(`${API_URL}/Storage/${page}`);
    const data = await res.json();
    data.forEach(f => {
        const li = document.createElement("li");
        const btn = document.createElement("button");
        btn.textContent = "Download";
        files.set(f.Id, f);
        btn.addEventListener("click", () => downloadFile(f.Id));
        li.textContent = `${f.FileName} (${f.Size} bytes)`;
        li.appendChild(btn);
        list.appendChild(li);
    });
}

async function downloadFile(fileId) {
    window.location = `${API_URL}/Storage/download/${fileId}`;
}

loadFiles(currentPage);

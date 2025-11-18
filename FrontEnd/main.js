const files = new Map();
let currentPage = 1;
const API_URL = window.location.origin.replace("3000", "5000");

async function uploadFile() {
    const fileInput = document.getElementById("fileInput");
    const formData = new FormData();
    const loader = document.getElementById("uploadIndicator");
    const uploadButton = document.getElementById("uploadbtn");
    uploadButton.disabled = true;
    loader.style.display = "block";
    formData.append("formFile", fileInput.files[0]);
    await fetch(`${API_URL}/Storage`, {
        method: "POST",
        body: formData
    });
    loader.style.display = "none";
    uploadButton.disabled = false;
    loadFiles();
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

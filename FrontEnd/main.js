const files = new Map();
let currentPage = 1;

async function uploadFile() {
    const fileInput = document.getElementById("fileInput");
    const formData = new FormData();
    formData.append("formFile", fileInput.files[0]);

    await fetch("http://localhost:5000/storage", {
        method: "POST",
        body: formData
    });

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

    const res = await fetch(`http://localhost:5000/Storage/${page}`);
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
    window.location = `http://localhost:5000/Storage/download/${fileId}`;
}

loadFiles(currentPage);

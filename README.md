# BitNest – Selfhosted Cloud auf Raspberry Pi

Ein kleines Selfhosted-Cloud-Projekt, das lokale Dateien verwaltet, Metadaten in PostgreSQL speichert und automatisch über Docker und GitHub Actions deployt wird.

---

## 🟢 MVP (Minimal Viable Product)

**Funktionalität:**
- Speicherung von Dateien als Blobs auf der Festplatte.
- PostgreSQL für Metadaten:
    - Dateiname
    - Dateiendung
    - Größe
    - Pfad/Link zum Blob
- REST-API Endpoints:
    - `GET /Storage/{page}` – Liste aller Dateien mit Metadaten (paginiert)
    - `GET /Storage/download/{id}` – Datei herunterladen
    - `POST /Storage` – Datei hochladen
    - `DELETE /Storage/{id}` – Datei löschen (Backend implementiert)
- Web-Frontend:
    - Vanilla JavaScript Frontend
    - Dateien hochladen
    - Dateien herunterladen
    - Paginierte Dateiliste
- Dockerized Setup:
    - .NET API Container
    - PostgreSQL Container
    - Frontend Container (Nginx)
    - Docker Volumes für persistente Daten
- Automatisiertes Deployment:
    - GitHub Actions CI/CD Pipeline
    - Baut Docker Images für API und Frontend
    - Multi-Platform Support (linux/amd64, linux/arm64)
    - Pusht Images zu Docker Hub
    - Deploy über `docker-compose up -d`
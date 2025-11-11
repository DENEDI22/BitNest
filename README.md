# BitNest – Selfhosted Cloud auf Raspberry Pi

Ein kleines Selfhosted-Cloud-Projekt, das lokale Dateien verwaltet, Metadaten in PostgreSQL speichert und automatisch über Docker und Jenkins deployt wird.

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
    - `GET /files` – Liste aller Dateien mit Metadaten
    - `GET /download/{pfad}` – Datei herunterladen
    - `POST /upload/{pfad}` – Datei hochladen
    - `DELETE /files/{id}` – Datei löschen
- Dockerized Setup:
    - .NET API Container
    - PostgreSQL Container
    - Bind Mounts / Volumes für persistente Daten
- Automatisiertes Deployment:
    - Jenkins pipeline prüft Tag im Git-Repo
    - Baut Release-Version
    - Baut Docker-Image
    - Überträgt Image auf Raspberry Pi per SSH
    - Deploy über `docker-compose up -d`

---

## ⚡ Zusätzliche Features / Zukunftsausbau

- Minimaler Web-UI Client (Razor Pages oder Blazor Server):
    - Dateien hochladen
    - Dateien herunterladen
    - Verzeichnisstruktur anzeigen
- Erweiterte API Features:
    - Suche nach Dateiname oder Endung
    - Pagination für große Dateimengen
- WebDAV-Support für externe Clients
- Authentifizierung / Berechtigungen:
    - Basic Auth oder JWT
- Lokales Docker Registry für einfachere Imagesynchronisation
- Automatische Backups von Metadaten und Blobs
- Monitoring / Logging Dashboard (z. B. über Portainer)


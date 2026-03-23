# BitNest – Selfhosted Cloud auf Raspberry Pi

Ein kleines Selfhosted-Cloud-Projekt, das lokale Dateien verwaltet, Metadaten in PostgreSQL speichert und automatisch über Docker und GitHub Actions deployt wird.

---

## Aktueller Stand: v0.0.3-alpha Auth + Sharepoint ✓

Alle geplanten Features für diesen Milestone sind implementiert.

---

## Implementierte Features

### Datei-Verwaltung (MVP)
- Speicherung von Dateien als Chunks auf der Festplatte (mit Deduplizierung)
- PostgreSQL für Metadaten (Dateiname, Endung, Größe, Pfad)
- REST-API Endpoints:
  - `GET /Storage/{page}` – Liste aller Dateien mit Metadaten (paginiert)
  - `GET /Storage/download/{id}` – Datei herunterladen
  - `POST /Storage` – Datei hochladen
  - `DELETE /Storage/{id}` – Datei löschen
- Vanilla JavaScript Frontend mit Upload, Download und paginierter Dateiliste

### Authentifizierung & Sessions (Phase 6)
- Benutzer-Registrierung, Login und Logout
- JWT Access Token + Refresh Token Rotation
- Automatische Auth-Gate beim Seitenstart im Frontend

### Benutzerverwaltung & Zugriffskontrolle (Phase 7)
- Admin-Bereich: Benutzer auflisten, deaktivieren, neu anlegen
- Dateimetadaten werden nur für eigene oder freigegebene Dateien zurückgegeben
- Download und Delete lehnen Zugriff für nicht autorisierte Benutzer ab

### Sharepoint – Ablaufende Download-Links (Phase 8)
- Authentifizierte Benutzer können temporäre Sharepoint-Links für ausgewählte Dateien erstellen
- Links haben ein konfigurierbares Ablaufdatum
- Öffentlicher Download nur über gültige, nicht abgelaufene Links
- Verwaltungsübersicht aktiver Links im Frontend

### Sharepoint – Dropbox-Upload (Phase 9)
- Öffentliche Benutzer können Dateien über gültige Sharepoint-Links hochladen (Dropbox-Stil)
- Upload-Slots mit Kapazitätsgrenze (atomare Durchsetzung im Backend)
- Uploads werden dem Link-Besitzer zugeordnet
- Eigene öffentliche Upload-Seite (`upload.html`)

---

## Deployment

- **Dockerized Setup:**
  - .NET API Container
  - PostgreSQL Container
  - Frontend Container (Nginx)
  - Docker Volumes für persistente Daten
- **Automatisiertes Deployment:**
  - GitHub Actions CI/CD Pipeline
  - Multi-Platform Support (linux/amd64, linux/arm64)
  - Push zu Docker Hub
  - Deploy über `docker-compose up -d`

---

## Tech Stack

- Backend: ASP.NET Core (`net9.0`), EF Core, Npgsql
- Frontend: Statisches HTML/CSS/JS
- Datenbank: PostgreSQL
- Deployment: Docker Compose, GitHub Actions

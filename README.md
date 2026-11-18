# RaceDay — PROG6212 Programming 2B POE

**Status:** Part 1 — System Planning and Database ✅ | Part 2 — RESTful API ✅ | Part 3 — MVC Web Application, Azure Blob Storage and Docker ✅

**Repository:** https://github.com/ST10466873/PROG6212-POE

---

## 1. System description

RaceDay is a full-stack, API-driven event management platform for South Africa's running, walking and cycling community. Organisers publish events, categories and race results; Participants browse upcoming events, enter a category, track their entries and view their personal race history.

The solution is built progressively:

| Part | Deliverable | Location |
|---|---|---|
| 1 | ERD, 22-endpoint API plan, SQL script and integrity tests | `docs/` |
| 2 | ASP.NET Core Web API (.NET 10), EF Core, BCrypt sessions, Swagger, xUnit | `src/RaceDay.Api/`, `tests/RaceDay.Tests/` |
| 3 | ASP.NET Core MVC web app, Azure Blob Storage integration, Docker | `src/RaceDay.Web/`, `Dockerfile`, `Dockerfile.api`, `docker-compose.yml` |

The MVC application is a **thin client**: it contains no DbContext, no entity classes and no duplicated business rules. All data operations, authorisation, image storage and aggregation happen in the Part 2 API.

---

## 2. Roles and access control

| Role | Capabilities |
|---|---|
| **Organiser** | Dashboard with event/enrolment/upcoming summaries; create, edit and delete events; upload event banners; manage age/distance categories; view all enrolments; capture and correct finish times and positions. |
| **Participant** | Register and log in; browse and filter upcoming events; view event detail and categories; enter an event by choosing a category; view and cancel personal entries; view personal results with finish time, position and field size; update profile and upload a profile picture. |

Role enforcement exists at both layers: the API validates every session token and role, and the MVC `RequireSessionAttribute` redirects anonymous users and blocks the opposite role from protected pages. The shared navigation renders a different menu for each role.

---

## 3. Repository structure

```text
/
├── README.md                          # This file
├── RaceDay.slnx                       # API + Web + Tests
├── Dockerfile                         # MVC web image (multi-stage, non-root)
├── Dockerfile.api                     # API image (multi-stage, non-root)
├── docker-compose.yml                 # web + api + SQL Server stack
├── .github/workflows/
│   ├── ci-api.yml                     # Builds API and MVC, runs all tests, validates structure
│   └── validate-structure.yml         # Part 1 artefact validation
├── docs/
│   ├── RaceDay_ERD.png / .svg         # Part 1 ERD
│   ├── api-endpoint-plan.md           # 22 Part 2 endpoints + Part 3 additive endpoints
│   ├── RaceDayDatabase.sql            # Part 1 schema + seed data
│   ├── part3-schema-extension.sql     # Documented Part 3 image URL columns
│   └── tests/RaceDay_Database_Tests.sql
├── src/RaceDay.Api/                   # Part 2 API + Part 3 API extensions
│   ├── Controllers/                   # Auth, Users, Events, Categories, Enrolments, Results,
│   │                                  #   Organisers (dashboard), Uploads (images/files)
│   ├── Services/AuthService.cs        # BCrypt hashing + 24-hour server-side sessions
│   ├── Services/ImageStorageService.cs# Azure Blob Storage with local development fallback
│   ├── DTOs/, Models/, Data/, Helpers/
│   └── Program.cs
├── src/RaceDay.Web/                   # Part 3 MVC application
│   ├── Controllers/                   # Account, Home, Events, Organiser, Participant, Media
│   ├── Filters/RequireSessionAttribute.cs
│   ├── Services/ApiClient.cs          # Single API gateway used by every controller
│   ├── Services/SessionExtensions.cs  # Server-side MVC session state
│   ├── Models/WebModels.cs            # View models mirroring API JSON only
│   ├── Views/                         # Role-aware Razor views + shared layout
│   ├── wwwroot/css/site.css           # RaceDay brand system
│   └── wwwroot/img/                   # Event placeholder artwork
└── tests/RaceDay.Tests/               # 23 xUnit tests (16 Part 2 + 7 Part 3)
```

---

## 4. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://learn.microsoft.com/sql/sql-server/) — LocalDB is sufficient. The API falls back to an in-memory store when no SQL Server is reachable.
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — for the containerised stack
- [Azure Storage account](https://learn.microsoft.com/azure/storage/blobs/) — **optional**; without a connection string the API uses a documented local image store so the app still runs end-to-end

---

## 5. Part 1 and Part 2 quick reference

### 5.1 Planning artefacts

- ERD: [`docs/RaceDay_ERD.png`](docs/RaceDay_ERD.png) ([editable SVG](docs/RaceDay_ERD.svg))
- Endpoint plan: [`docs/api-endpoint-plan.md`](docs/api-endpoint-plan.md) — all 22 Part 2 endpoints plus the five additive Part 3 endpoints, with request bodies and response codes
- Database: [`docs/RaceDayDatabase.sql`](docs/RaceDayDatabase.sql) — 8 tables, constraints and seed data
- Database tests: [`docs/tests/RaceDay_Database_Tests.sql`](docs/tests/RaceDay_Database_Tests.sql)

Run the SQL script and test suite in SSMS with **F5**. The expected final line is `ALL TESTS PASSED`.

### 5.2 API

```bash
dotnet run --project src/RaceDay.Api
```

- Swagger: `http://localhost:5049/swagger`
- Health: `http://localhost:5049/health`
- Session token header: `X-Session-Token: <token>` (Bearer is also accepted)
- Passwords are BCrypt-hashed; server-side sessions expire after 24 hours

Run all 23 tests:

```bash
dotnet test
```

Test coverage includes registration/login, role enforcement, event CRUD, enrolments, results, the Organiser dashboard, personal race history, banner/profile uploads, ownership checks and upload validation.

---

## 6. Part 3 — MVC web application

### 6.1 Run locally (two terminals)

```bash
# Terminal 1 — API
dotnet run --project src/RaceDay.Api

# Terminal 2 — MVC web app
dotnet run --project src/RaceDay.Web
```

Open **http://localhost:5080**. The MVC app is configured with `ApiSettings:BaseUrl = http://localhost:5049` in `src/RaceDay.Web/appsettings.json`.

### 6.2 Run the whole stack with Docker (single command)

```bash
docker compose up --build
```

Then open:

| Service | URL |
|---|---|
| MVC web application | http://localhost:5080 |
| API + Swagger | http://localhost:5001/swagger |
| SQL Server | localhost:1433 |

`docker compose down` stops the stack; `docker compose down -v` also removes the database volume.

To run just the web image (the API must already be reachable):

```bash
docker build -t raceday-web .
docker run --rm -p 5080:8080 -e ApiSettings__BaseUrl=http://host.docker.internal:5049 raceday-web
```

Both Dockerfiles use multi-stage builds, restore-layer caching, .NET 10 images and a non-root runtime user. Inside Compose the web container reaches the API at the internal address `http://api:8080`, so the browser never needs to resolve the API container.

### 6.3 Pages and navigation

| Role | Pages |
|---|---|
| Public | Home / browse events with type + location filters, event detail with categories and published leaderboard, register, login |
| Organiser | Dashboard (totals, upcoming dates, enrolments per event), create/edit/delete events with banner upload, category management, event enrolment list, results entry/correction |
| Participant | Event detail with **Enter Event**, My Events with colour-coded status and cancellation, My Results with "47th out of 312 finishers", profile edit and profile-picture upload |

Every page uses the same Bootstrap 5 layout, RaceDay colour system, typography and status badges defined in `wwwroot/css/site.css`. Invalid input is shown as a validation message or an alert — never as a raw exception or JSON error document.

### 6.4 Session-based authentication

1. The user posts credentials to the MVC `AccountController`.
2. MVC forwards them to `POST /api/auth/login` and receives an opaque API session token.
3. MVC stores the token, user ID, role and name in the **server-side ASP.NET Core session**; only the session cookie is sent to the browser.
4. Every subsequent `ApiClient` request attaches `X-Session-Token`.
5. `RequireSession` protects pages; unauthenticated users are redirected to login with a return URL, and the wrong role is redirected to an access-denied page.
6. Logout calls `POST /api/auth/logout` (invalidating the API session) and then clears the MVC session.

---

## 7. Azure Blob Storage integration

The API alone owns file storage. The MVC app posts `multipart/form-data` to the API and receives a URL back; it never holds a storage connection string and never contacts Azure.

| Endpoint | Purpose |
|---|---|
| `POST /api/events/{id}/banner` | Organiser (owner only) uploads an event banner; URL stored on `Events.BannerImageUrl` |
| `POST /api/users/me/profile-picture` | Signed-in user uploads a profile picture; URL stored on `Users.ProfilePictureUrl` |
| `GET /api/files/{path}` | Public read proxy for locally stored development images |
| `GET /api/organisers/dashboard` | Aggregated Organiser dashboard data |
| `GET /api/enrolments/me/results` | Participant race history with field size |

Supported images: JPG, PNG, WEBP, GIF up to 5 MB. Uploads are rejected with `400` otherwise.

### Configuration

Set a real Azure connection string to enable blob storage; otherwise the API automatically uses its local development store:

```bash
# PowerShell / Docker Compose environment variable
$env:AzureStorage__ConnectionString = "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
$env:AzureStorage__ContainerName = "raceday-images"
```

In `docker-compose.yml` these map to `AZURE_STORAGE_CONNECTION_STRING` and `AZURE_STORAGE_CONTAINER`. The response field `storage` reports `AzureBlobStorage` or `LocalDevelopment`, so the demo is reproducible without cloud credentials.

The two new nullable columns are documented in [`docs/part3-schema-extension.sql`](docs/part3-schema-extension.sql). This is the deliberate, documented difference between the original Part 1 schema and the final Code-First model.

---

## 8. Continuous integration

![CI green build](https://github.com/user-attachments/assets/7f213046-e9a3-4aee-ba94-13ca8927d30b)

GitHub Actions (`.github/workflows/ci-api.yml`) runs on every push and pull request:

1. Sets up .NET 10 and restores the whole solution.
2. Builds the API, MVC web app and tests in Release.
3. Runs all 23 xUnit tests.
4. Publishes both applications to verify container-ready artefacts.
5. Re-validates the Part 1 docs and the Part 3 MVC/Docker structure.

`validate-structure.yml` separately checks the Part 1 deliverables. Both workflows show green checks on `main`. Capture a fresh screenshot after your final push and replace the image above if the badge/date differs.

---

## 9. Video presentation

**Submit an unlisted YouTube link here before the deadline:** `VIDEO_LINK_TODO`

The video should demonstrate the running MVC application, explain the MVC structure, show both role interfaces, walk through the Azure Blob Storage flow through the API, and explain the Docker setup. A voiceover recorded in your own voice is required; AI-generated voices are not permitted.

---

## 10. AI tool disclosure

An AI coding assistant was used to help draft and review parts of the Part 3 implementation and documentation. All code was reviewed, run and verified locally by the student (build, 23 passing tests and CI); no AI-generated voiceover was used.

---

## 11. References

- Microsoft. (n.d.). *ASP.NET Core documentation*. https://learn.microsoft.com/aspnet/core/
- Microsoft. (n.d.). *Entity Framework Core*. https://learn.microsoft.com/ef/core/
- Microsoft. (n.d.). *Azure Blob Storage documentation*. https://learn.microsoft.com/azure/storage/blobs/
- Microsoft. (n.d.). *Session state in ASP.NET Core*. https://learn.microsoft.com/aspnet/core/fundamentals/app-state
- Docker. (n.d.). *Docker documentation*. https://docs.docker.com/
- Bootstrap. (n.d.). *Bootstrap 5 documentation*. https://getbootstrap.com/docs/5.3/
- xUnit.net. (n.d.). *xUnit.net*. https://xunit.net/
- Swashbuckle. (n.d.). *Swashbuckle.AspNetCore*. https://github.com/domaindrivendev/Swashbuckle.AspNetCore
- Medo, A. (n.d.). *BCrypt.Net-Next*. https://github.com/BcryptNet/bcrypt.net

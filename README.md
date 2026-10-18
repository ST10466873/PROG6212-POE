# RaceDay

**PROG6212 – Programming 2B | Portfolio of Evidence (POE)**  
**Current Status:** Part 1 – System Planning and Database ✅ | Part 2 – RESTful API ✅ | Part 3 – MVC Web Application (planned)

---

## 1. System Description

**RaceDay** is a full-stack web-based event management system built for the South African running, walking and cycling community — from major events like the Comrades Marathon and Two Oceans, to weekend park runs and charity cycles. Many organisers still rely on paper-based registration, spreadsheets and disconnected communication channels.

RaceDay replaces this with a single API-driven platform where **Event Organisers** can create and manage events, categories and participant results, while **Participants** can browse upcoming events, enrol by category, track their personal history and view event results. The system has been designed to be extensible to an MVC web interface in Part 3 and is fully containerised.

---

## 2. Roles and Access Control

Role-based access is enforced at the API layer (Part 2) and will be consistently applied in the MVC interface (Part 3).

| Role | Capabilities |
|---|---|
| **Organiser** | Create, update and delete their own events; manage event categories (age and distance-based); capture and correct participant results (finish times and positions); view all enrolments for their events. |
| **Participant** | Register an account; browse events and categories; enrol in events by selecting a category; view their own enrolments; cancel their own enrolments; view their own results or public event result tables. |

---

## 3. Repository Structure

```text
/
├── README.md                        # This file (submission overview)
├── RaceDay.slnx                     # Solution containing API and test projects
├── Dockerfile                       # API container build
├── docker-compose.yml               # API + SQL Server stack
├── .github/workflows/
│   ├── validate-structure.yml       # CI: validates Part 1 documentation structure
│   └── ci-api.yml                   # CI: builds API and runs all tests
├── docs/
│   ├── RaceDay_ERD.png              # Section A: Entity Relationship Diagram
│   ├── RaceDay_ERD.svg              # Editable ERD source
│   ├── api-endpoint-plan.md         # Section B: 22-endpoint RESTful API plan
│   ├── RaceDayDatabase.sql          # Section C: Complete SQL schema + seed data
│   └── tests/
│       └── RaceDay_Database_Tests.sql # 22 database integrity tests
├── src/RaceDay.Api/                 # Part 2: ASP.NET Core Web API (.NET 10)
│   ├── Controllers/                 # 6 controllers (Auth, Users, Events, Categories, Enrolments, Results)
│   ├── Models/                      # 8 domain entities matching the ERD (Role, User, Event, Route, EventCategory, Enrolment, Result, Session)
│   ├── Data/RaceDayContext.cs       # EF Core DbContext with PKs, FKs, UQs, CKs and seed data
│   ├── DTOs/AuthDtos.cs             # Request/response DTOs mapped to all endpoints
│   ├── Services/AuthService.cs      # BCrypt.Net-Next hashing + session token generation/validation
│   ├── Helpers/SessionHelper.cs     # Session extraction (X-Session-Token or Bearer) + 401/403 helpers
│   ├── Program.cs                   # DI, Swagger, DB creation, health check, InMemory/SQL selection
│   ├── appsettings.json             # Connection strings (LocalDB default)
│   └── RaceDay.Api.http             # HTTP request samples for testing
└── tests/RaceDay.Tests/             # Part 2: xUnit test suite (16 tests)
    ├── TestHelpers.cs               # Factory + helpers for isolated test DBs
    ├── AuthTests.cs                 # 4 auth/session tests
    ├── EventTests.cs                # 4 event management tests
    ├── EnrolmentTests.cs            # 4 enrolment/category/result logic tests
    └── ApiIntegrationTests.cs       # 4 integration tests via WebApplicationFactory
```

---

## 4. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) installed
- [SQL Server](https://learn.microsoft.com/en-us/sql/sql-server/) (LocalDB is sufficient, e.g. `(localdb)\MSSQLLocalDB`) — the API automatically falls back to InMemory if SQL Server is unavailable (used by tests)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (optional, for containerised run)
- [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/en-us/sql/ssms/) (optional, to run Part 1 SQL scripts)

---

## 5. Part 1 – Database Setup and Validation

The planning and database artefacts are located in `/docs/`.

### 5.1 View Planning Documents
- **ERD:** [docs/RaceDay_ERD.png](docs/RaceDay_ERD.png) (also [SVG](docs/RaceDay_ERD.svg))
- **API Endpoint Plan:** [docs/api-endpoint-plan.md](docs/api-endpoint-plan.md) – documents all 22 endpoints, roles, rules, status codes and example payloads
- **Database Script:** [docs/RaceDayDatabase.sql](docs/RaceDayDatabase.sql) – full DDL + DML (seed data)
- **Database Tests:** [docs/tests/RaceDay_Database_Tests.sql](docs/tests/RaceDay_Database_Tests.sql) – 22 integrity tests

### 5.2 Execute the SQL Schema (SSMS)
1. Open **SQL Server Management Studio** and connect to your instance (e.g. `localhost` or `.\SQLEXPRESS`).
2. Go to **File → Open → File…** and select [`docs/RaceDayDatabase.sql`](docs/RaceDayDatabase.sql).
3. Execute with **F5**. This will create the `RaceDay` database with 8 tables, all constraints (PKs/FKs/UQs/CKs), indexes and seed data.
4. Verify with: `USE RaceDay; SELECT * FROM dbo.Events;`

### 5.3 Run the Database Integrity Tests (SSMS)
1. Open [`docs/tests/RaceDay_Database_Tests.sql`](docs/tests/RaceDay_Database_Tests.sql) in SSMS.
2. Execute with **F5**. The script prints per-test results and should conclude with: **`ALL TESTS PASSED`**.

**Seed Data (as defined):** 2 Organisers, 4 Participants, 3 Events, 3 Routes, 9 Categories, 7 Enrolments, 5 Results, 2 Sessions.

---

## 6. Part 2 – RESTful API (.NET 10)

The API implements **exactly 22 endpoints** as specified in [docs/api-endpoint-plan.md](docs/api-endpoint-plan.md). All constraints from the ERD are enforced (unique emails/events/categories/enrolments/positions, CHECKs for roles/event types/distances, ownership checks and age bounds).

### 6.1 Configuration
Connection string (default):  
`Server=(localdb)\MSSQLLocalDB;Database=RaceDay;Trusted_Connection=True;TrustServerCertificate=True`  
Defined in [`src/RaceDay.Api/appsettings.json`](src/RaceDay.Api/appsettings.json). If LocalDB is unavailable and no connection string is provided, the API uses InMemory. In `Testing` environment (xUnit), an isolated InMemory database is used per test host.

Authentication: server-side **Sessions** table. Tokens are 64-hex strings (32 random bytes) with a 24-hour expiry. Clients send the token via `X-Session-Token: <token>` or `Authorization: Bearer <token>`. Passwords are hashed using **BCrypt.Net-Next 4.0.3** (cost 11).

### 6.2 Run the API – Visual Studio
1. Open [`RaceDay.slnx`](RaceDay.slnx) in Visual Studio 2022/2026.
2. Set **`RaceDay.Api`** as the startup project.
3. Press **F5** (HTTPS). Swagger UI will open at `https://localhost:5001/swagger`.
4. Health endpoint: `https://localhost:5001/health` returns `{ status: "ok", timestamp: ... }`.

### 6.3 Run the API – .NET CLI
```bash
# From repository root
dotnet restore
dotnet build
dotnet run --project src/RaceDay.Api
```

- Swagger: `https://localhost:5001/swagger`  
- Root redirects to `/swagger`  
- Health: `https://localhost:5001/health`

### 6.4 Run the API – Docker Compose
```bash
docker compose up --build
```

- API: `http://localhost:5001`
- SQL Server: `localhost:1433` (SA / `RaceDay123!` as configured in `docker-compose.yml`)
- Swagger: `http://localhost:5001/swagger`

### 6.5 Run Tests
```bash
dotnet test
```

**Test Results (16/16 passing):**
- **AuthTests.cs** (4) – Registration validation, duplicate email (409), login success/failure, BCrypt cost verification
- **EventTests.cs** (4) – Event creation (organiser), duplicate prevention (409), update/delete ownership (403), public listing
- **EnrolmentTests.cs** (4) – Enrolment rules (participant only), duplicate enrolment (409), organiser views enrolments, age/category constraints, result uniqueness (409 on duplicate position)
- **ApiIntegrationTests.cs** (4) – Full HTTP integration via `WebApplicationFactory<Program>` covering registration/login, event creation, enrolment and results flow across the 22 endpoints

All tests run against isolated InMemory databases; no external SQL Server is required.

### 6.6 Quick API Flow (Manual Test)
1. **Register Organiser** → `POST /api/auth/register` (`Role: "Organiser"`)
2. **Login** → `POST /api/auth/login` → copy `sessionToken`
3. **Authorise in Swagger** → Add `X-Session-Token` header (or Bearer)
4. **Create Event** → `POST /api/events`
5. **Add Category** → `POST /api/events/{id}/categories`
6. **Register Participant** → separate session
7. **Enrol** → `POST /api/events/{id}/enrolments` (Participant)
8. **Capture Result** → back as Organiser → `POST /api/enrolments/{id}/result`
9. **View Public Results** → `GET /api/events/{id}/results` (no auth required)

Ready-to-send requests are provided in [`src/RaceDay.Api/RaceDay.Api.http`](src/RaceDay.Api/RaceDay.Api.http).

---

## 7. Complete Endpoint Reference (22 Endpoints)

All endpoints match [`docs/api-endpoint-plan.md`](docs/api-endpoint-plan.md) exactly. Authenticated routes require a valid session token.

| Area | Method | Route | Auth | Roles | Description |
|---|---|---|---|---|---|
| **Auth** | `POST` | `/api/auth/register` | No | Any | Register new Organiser or Participant. Hashes password with BCrypt. Returns 409 if email exists. |
| **Auth** | `POST` | `/api/auth/login` | No | Any | Validates credentials. Creates session (64-hex token, 24h expiry). Returns token + user info. |
| **Auth** | `POST` | `/api/auth/logout` | Yes | Any | Invalidates/deactivates current session. |
| **Users** | `GET` | `/api/users/me` | Yes | Any | Returns authenticated user's profile. |
| **Users** | `PUT` | `/api/users/me` | Yes | Any | Updates own profile. Returns 409 if email is taken by another user. |
| **Events** | `GET` | `/api/events` | No | Public | Lists all events. Supports optional filters: `?eventType=Run&location=Cape%20Town`. |
| **Events** | `GET` | `/api/events/{id}` | No | Public | Returns event detail including optional `Route` (1:1). 404 if not found. |
| **Events** | `POST` | `/api/events` | Yes | Organiser | Creates event owned by current organiser. 409 if (Name, Date, Location) duplicate. |
| **Events** | `PUT` | `/api/events/{id}` | Yes | Organiser | Updates own event. 403 if not owner. 409 on duplicate. |
| **Events** | `DELETE` | `/api/events/{id}` | Yes | Organiser | Deletes own event (cascades route/categories/enrolments/results). 403 if not owner. |
| **Categories** | `GET` | `/api/events/{id}/categories` | No | Public | Lists all categories for an event. 404 if event missing. |
| **Categories** | `POST` | `/api/events/{id}/categories` | Yes | Organiser | Adds category to owned event. 400 on invalid age range; 409 if (EventId, CategoryName) duplicate. |
| **Categories** | `PUT` | `/api/categories/{id}` | Yes | Organiser | Updates category. Enforces ownership via event. 400 on invalid age range; 409 on duplicate name. |
| **Categories** | `DELETE` | `/api/categories/{id}` | Yes | Organiser | Deletes category if no enrolments exist. 409 if enrolments reference it. |
| **Enrolments** | `POST` | `/api/events/{id}/enrolments` | Yes | Participant | Enrols current participant in event with selected `CategoryId`. 409 if already enrolled. 400 if age/category invalid. |
| **Enrolments** | `GET` | `/api/events/{id}/enrolments` | Yes | Organiser | Lists all enrolments for owned event (includes participant + category names). 403 if not owner. |
| **Enrolments** | `GET` | `/api/enrolments/me` | Yes | Participant | Lists current participant's own enrolments (with event/category names). |
| **Enrolments** | `DELETE` | `/api/enrolments/{id}` | Yes | Participant | Cancels own enrolment. 403 if another participant's enrolment. |
| **Results** | `POST` | `/api/enrolments/{id}/result` | Yes | Organiser | Captures finish time + position for an enrolment. Enforces 1:1 per enrolment and unique position per event. 409 if duplicate. |
| **Results** | `PUT` | `/api/results/{id}` | Yes | Organiser | Corrects existing result (time/position). Excludes self from position conflict check. 409 on position conflict. |
| **Results** | `GET` | `/api/enrolments/{id}/result` | Yes | Any | Returns result for an enrolment. Allowed if requestor is the participant or the owning organiser. 404 if not captured. |
| **Results** | `GET` | `/api/events/{id}/results` | No | Public | Returns event results ordered by `FinishingPosition` ascending (podium-first). 404 if event missing. |

---

## 8. Code Quality & Documentation

- **XML Documentation (`///`):** All models, DTOs, services, helpers and controllers include detailed `<summary>` comments for classes, methods and properties.
- **Inline Comments (`//`):** Business rules, constraints, validation guards and ownership checks are clearly annotated throughout the codebase.
- **Comment Coverage (excluding `obj/`):** **488 lines containing `//`** total, including **412 pure comment-start lines** and **288 XML `///` documentation lines**.
- **Defensive Validation:** Ownership checks on every organiser-scoped operation, role enforcement, uniqueness (409), not found (404), forbidden (403), bad request (400).
- **Separation of Concerns:** Controllers thin, business/auth logic in `AuthService`, session helpers in `Helpers`, EF mapping in `Data/RaceDayContext`.

---

## 9. Continuous Integration (CI)

GitHub Actions workflows run on push, pull request and manual dispatch:

- **[validate-structure.yml](.github/workflows/validate-structure.yml)** – Re-validates Part 1 artefacts: ERD, endpoint plan, SQL script, and minimum table count (≥6). Runs on Ubuntu.
- **[ci-api.yml](.github/workflows/ci-api.yml)** – Builds and tests the API in Release configuration on Ubuntu with .NET 10. Also re-checks critical docs (ERD, endpoint plan, SQL script, table count). All 16 xUnit tests must pass.

Both workflows are green on `main`.

---

## 10. Part 3 – MVC Web Application (Planned)

Part 3 will extend RaceDay with an ASP.NET Core MVC web application in `src/RaceDay.Web`. The MVC layer will consume the same domain rules, enforce role-based UI (Organiser vs Participant), and provide forms/views for events, categories, enrolments and results. This README will be updated to include MVC setup/run instructions once implemented.

---

**Repository:** [https://github.com/ST10466873/PROG6212-POE](https://github.com/ST10466873/PROG6212-POE)  
**Solution:** `RaceDay.slnx` (contains `RaceDay.Api` and `RaceDay.Tests`)

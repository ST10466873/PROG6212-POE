# RaceDay

**PROG6212 — Programming 2B · Portfolio of Evidence**
**Current status:** Part 1 — System Planning and Database ✅ · Part 2 — RESTful API ✅ · Part 3 — MVC Web Application (planned)

---

## 1. System description

**RaceDay** is a full-stack, web-based event management system built for the South African road running, walking and cycling community — from the Comrades Marathon and Two Oceans down to weekend park runs and charity cycles. Despite the enormous participation these events attract, many organisers still manage them through paper-based registration, spreadsheets and disconnected communication channels.

RaceDay replaces that with a single platform where **Event Organisers** create and manage events, categories and participant results, while **Participants** browse upcoming events, enter them by choosing a category, track their personal performance history, and prepare for race day using live weather and route information. The system is designed up front to be API-driven, cloud-aware and fully containerised by the final part.

## 2. The two roles

| Role | Capabilities |
|---|---|
| **Organiser** | Create, edit and delete events; manage event categories (age and distance, e.g. Under 20, Senior, 10km, 21km); capture participant results (finish times and finishing positions); view all enrolments for their events. |
| **Participant** | Create an account; browse events; enter an event by selecting a category; view their own enrolments; track their personal results. |

Role-based access is enforced at the API level in Part 2 and reflected consistently in the MVC interface in Part 3.

## 3. Repository structure

```
/                               ← repository root
├── README.md                   ← this file
├── RaceDay.slnx                ← solution (API + tests)
├── Dockerfile                  ← container build for the API
├── docker-compose.yml          ← API + SQL Server
├── .github/workflows/
│   ├── validate-structure.yml  ← CI: validates Part 1 docs structure
│   └── ci-api.yml              ← CI: builds API and runs unit tests
├── docs/
│   ├── RaceDay_ERD.png         ← Section A: Entity Relationship Diagram
│   ├── RaceDay_ERD.svg         ← editable source of the ERD
│   ├── api-endpoint-plan.md    ← Section B: API endpoint plan (22 endpoints)
│   ├── RaceDayDatabase.sql     ← Section C: full SQL schema + seed data
│   └── tests/
│       └── RaceDay_Database_Tests.sql ← 22 database tests
├── src/RaceDay.Api/            ← Part 2: ASP.NET Core Web API
│   ├── Controllers/            ← Auth, Users, Events, Categories, Enrolments, Results
│   ├── Models/                 ← 8 entities matching the ERD
│   ├── Data/RaceDayContext.cs  ← EF Core DbContext with all constraints
│   ├── DTOs/                   ← request/response shapes per endpoint plan
│   ├── Services/AuthService.cs ← BCrypt hashing + session token management
│   ├── Helpers/SessionHelper.cs← X-Session-Token / Bearer extraction
│   ├── Program.cs              ← DI, Swagger, DB creation
│   └── appsettings.json        ← SQL Server connection (LocalDB default)
└── tests/RaceDay.Tests/        ← Part 2: xUnit suite (16 tests)
    ├── AuthTests.cs
    ├── EventTests.cs
    ├── EnrolmentTests.cs
    ├── ApiIntegrationTests.cs  ← 4 integration tests via WebApplicationFactory
    └── TestHelpers.cs
```

## 4. Setup instructions

### 4.1 View the planning documents (Part 1)
No tooling required — open `docs/api-endpoint-plan.md` and `docs/RaceDay_ERD.png` directly in GitHub.

### 4.2 Run the SQL script (Part 1 — SSMS)
1. Open **SQL Server Management Studio** and connect to `localhost` or `.\SQLEXPRESS`.
2. **File → Open → File…** → select `docs/RaceDayDatabase.sql` → **F5**.
3. Creates the `RaceDay` database, 8 tables, indexes and seed data (2 Organisers, 4 Participants, 3 Events, 3 routes, 9 categories, 7 enrolments, 5 results, 2 sessions).
4. Verify: `USE RaceDay; SELECT * FROM dbo.Events;`
5. Run `docs/tests/RaceDay_Database_Tests.sql` → expect `ALL TESTS PASSED`.

### 4.3 Run the API (Part 2 — Visual Studio or CLI)

**Prerequisites:** .NET 10 SDK, SQL Server (LocalDB or full). No LocalDB? The API falls back to InMemory automatically.

**Visual Studio 2022/2026:**
1. Open `RaceDay.slnx`.
2. Set `RaceDay.Api` as startup project.
3. Press **F5**. Swagger opens at `https://localhost:5001/swagger`.

**CLI:**
```bash
dotnet restore
dotnet build
dotnet run --project src/RaceDay.Api
# Swagger: https://localhost:5001/swagger
# Health:  https://localhost:5001/health
```

**Docker:**
```bash
docker compose up --build
# API on http://localhost:5001, SQL Server on 1433 (sa / RaceDay123!)
```

Connection string is in `src/RaceDay.Api/appsettings.json`:
`Server=(localdb)\MSSQLLocalDB;Database=RaceDay;Trusted_Connection=True;TrustServerCertificate=True`

### 4.4 Run the tests (Part 2)

```bash
dotnet test
# 16 tests: Auth (4) + Events (4) + Enrolments (4) + Integration (4) + unit
```

Tests use EF Core InMemory with isolated databases — no SQL Server required.

### 4.5 Test the API with Swagger or curl

1. **Register** `POST /api/auth/register` with `role: Organiser` or `Participant`.
2. **Login** `POST /api/auth/login` → copy `sessionToken`.
3. In Swagger click **Authorize** or add header `X-Session-Token: <token>` (or `Authorization: Bearer <token>`) for protected routes.
4. Flow demo: Organiser creates event → adds category → Participant enrols → Organiser captures result → public `GET /api/events/{id}/results`.

`.http` file at `src/RaceDay.Api/RaceDay.Api.http` contains ready-to-send requests.

## 5. API — 22 endpoints (matches `docs/api-endpoint-plan.md`)

| Area | Method | Route | Roles | Description |
|---|---|---|---|---|
| Auth | POST | `/api/auth/register` | None | Register Organiser/Participant, BCrypt hash |
| Auth | POST | `/api/auth/login` | None | Login, returns 64-char session token (24h) |
| Auth | POST | `/api/auth/logout` | Any | Invalidate session |
| Users | GET | `/api/users/me` | Any | Own profile |
| Users | PUT | `/api/users/me` | Any | Update own profile (409 on duplicate email) |
| Events | GET | `/api/events` | None | List + filter by type/location |
| Events | GET | `/api/events/{id}` | None | Detail + route |
| Events | POST | `/api/events` | Organiser | Create owned event (409 on duplicate) |
| Events | PUT | `/api/events/{id}` | Organiser | Update own event (403 otherwise) |
| Events | DELETE | `/api/events/{id}` | Organiser | Delete own event |
| Categories | GET | `/api/events/{id}/categories` | None | List categories |
| Categories | POST | `/api/events/{id}/categories` | Organiser | Add category (400 on age/bad duplicate) |
| Categories | PUT | `/api/categories/{id}` | Organiser | Update category |
| Categories | DELETE | `/api/categories/{id}` | Organiser | Remove if no enrolments (409 otherwise) |
| Enrolments | POST | `/api/events/{id}/enrolments` | Participant | Enter event (409 if already enrolled) |
| Enrolments | GET | `/api/events/{id}/enrolments` | Organiser | All enrolments for owned event |
| Enrolments | GET | `/api/enrolments/me` | Participant | Own enrolments |
| Enrolments | DELETE | `/api/enrolments/{id}` | Participant | Cancel own enrolment |
| Results | POST | `/api/enrolments/{id}/result` | Organiser | Capture time+position (409 on duplicate position) |
| Results | PUT | `/api/results/{id}` | Organiser | Correct result |
| Results | GET | `/api/enrolments/{id}/result` | Any | Own / organiser view |
| Results | GET | `/api/events/{id}/results` | None | Ordered results table |

Error codes match the plan: `400` validation, `401` unauthenticated, `403` forbidden (wrong role / not owner), `404` not found, `409` conflict.

## 6. CI/CD

* **`validate-structure.yml`** — runs on every push/PR. Checks `/docs` deliverables, 6 required endpoint-plan columns, ≥6 tables + seed data, ≥10 DB tests, ERD vs SQL entity match. Produces the green badge in the README.
* **`ci-api.yml`** — builds the solution and runs `dotnet test` on `ubuntu-latest` with .NET 10. Fails the build if any of the 16 tests fail.

Both must be green — see Actions tab.

**Screenshots:**

*Part 1 validation* — `docs/images/ci-build-screenshot.png`
*Part 2 API tests* — trigger by pushing; badge at top of this README.

## 7. Marking deliverables checklist

### Part 1 — System Planning and Database ✅
- [x] `/docs` with ERD (PNG), endpoint plan (22 endpoints, 6 columns) and SQL script (8 tables)
- [x] ERD with 8 entities, PKs, FKs and cardinality
- [x] SQL script re-runnable on clean instance, matches ERD, with indexes + seed data
- [x] Database test suite — 22 tests, `ALL TESTS PASSED`, non-destructive
- [x] GitHub Actions workflow for structure validation
- [x] 20+ meaningful commits, green build

### Part 2 — RESTful API ✅
- [x] ASP.NET Core Web API implementing all 22 endpoints from the plan
- [x] EF Core with SQL Server (LocalDB) + InMemory fallback, 8 entities, FKs, unique and check constraints
- [x] Session-based authentication — `Sessions` table, 64-hex token, 24h expiry, `X-Session-Token` header, `BCrypt` (cost 11) hashing
- [x] Role-based authorization on every protected route (401/403) and ownership checks (Organiser owns event)
- [x] Validation matching the SQL constraints (duplicate email 409, duplicate event/category 400/409, invalid category 400, already enrolled 409, position taken 409, negative ages/distance 400)
- [x] Error handling with consistent JSON `{ message }` and correct status codes
- [x] Swagger UI at `/swagger` and `/health` endpoint
- [x] Dockerfile + docker-compose (API + SQL Server)
- [x] xUnit test suite — 16 tests (unit + integration via WebApplicationFactory, isolated InMemory DBs)
- [x] GitHub Actions `ci-api` workflow (build + test)
- [x] 12+ additional spaced commits for Part 2
- [x] README with setup, endpoint table and architecture

## 8. AI tooling disclosure

AI tooling (an AI coding assistant) was used while preparing this portfolio, as permitted by the module instructions, and is disclosed here in line with the assignment requirement.¹ All planning content — the data model, endpoint design, seed data choices and this README — was directed, reviewed, corrected and approved by me before being committed, and the same disclosure applies to Parts 2 and 3 where AI assistance is used for planning, proofreading or coding.

> ¹ Assignment instruction 3: *"If you used AI tools in any part of your process (e.g., planning, proofreading, coding), disclose their use briefly in a footnote or comments section."*

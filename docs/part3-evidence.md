# Part 3 assessment evidence

Use this checklist when recording the demo and when the marker opens the repository.

| Requirement | Evidence in repository | Demo action |
|---|---|---|
| MVC application consuming own API | `src/RaceDay.Web/Services/ApiClient.cs`; no `DbContext` or entity classes in the Web project; `docs/part3-deployment.md` | Open two terminals: API Swagger and the MVC site; show that editing an event updates Swagger-backed data |
| Correct UI design and navigation | `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css`, role-specific view folders | Show the colour-coded navbar and status badges for both roles |
| Session management, login and logout | `Services/SessionExtensions.cs`, `Filters/RequireSessionAttribute.cs`, `Controllers/AccountController.cs` | Log in as each role; try a protected page while signed out; log out and confirm the API session is invalid |
| Login redirect based on role | `AccountController.Login` | Show Organiser landing on `/Organiser/Dashboard` and Participant landing on `/Home/Index` |
| Organiser dashboard | `Controllers/OrganiserController.cs` + `Views/Organiser/Dashboard.cshtml` + `GET /api/organisers/dashboard` | Point to total events, total enrolments, upcoming dates and per-event counts |
| Organiser event CRUD and banner upload | `OrganiserController.Events/Create/Edit/Delete`, `POST /api/events/{id}/banner` | Create an event, upload a banner, edit it, then delete it |
| Organiser category management | `OrganiserController.Categories/SaveCategory/DeleteCategory` | Add a category and delete a category on an owned event |
| Organiser enrolments and results | `OrganiserController.Enrolments/Results`, `GET /api/events/{id}/enrolments`, result capture endpoint | Capture a result and show it appears on the public leaderboard and participant history |
| Participant browse, filter and event detail | `HomeController.Index`, `EventsController.Details` | Filter by event type and location, open an event, inspect categories and results |
| Participant enter and cancel | `EventsController.Enrol/Cancel` | Enter a category, show status badge, then cancel |
| Participant results display | `Views/Participant/MyResults.cshtml` + `GET /api/enrolments/me/results` | Show position and field size, e.g. "47th out of 312" |
| Profile view/update and picture upload | `ParticipantController.Profile`, `POST /api/users/me/profile-picture` | Update profile details and upload an avatar |
| Azure Blob Storage through API only | `src/RaceDay.Api/Services/ImageStorageService.cs`, `Controllers/UploadsController.cs` | Show `storage: AzureBlobStorage` response with a connection string; show local fallback without one; confirm the Web project has no Azure package |
| Docker containerisation | `Dockerfile`, `Dockerfile.api`, `docker-compose.yml` | Run `docker compose up --build`, open ports 5080 and 5001, show both containers healthy |
| Gracious handling of invalid input | Data annotations in `Models/WebModels.cs`, MVC alerts, API problem responses | Submit an empty form, a future date of birth, and a `.exe` upload; show friendly messages |
| CI builds MVC and runs tests | `.github/workflows/ci-api.yml` | Show green checks on the pushed commit and the screenshot in README section 8 |
| 20 meaningful commits for Part 3 | `git log --oneline --grep='^Part 3'` | Run the command and show the Part 3 commit list |
| README with Docker run command | README section 6.2 | Show `docker compose up --build` and the single-web-image `docker run` command |
| YouTube demo link | README section 9 | Replace `VIDEO_LINK_TODO` with the unlisted link before submission |

## Verification commands

```bash
dotnet build RaceDay.slnx --configuration Release
dotnet test RaceDay.slnx --no-build --configuration Release --verbosity normal
git log --oneline --grep='^Part 3'
```

Expected result: 0 build warnings, 0 build errors, 38 passing tests.

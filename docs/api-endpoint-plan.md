# RaceDay — API Endpoint Plan

**Module:** PROG6212 — Programming 2B
**Part:** Part 1 — System Planning and Database (Section B)
**Status:** Approved plan — the API implemented in Part 2 must closely match this plan.

All routes are relative to the API base URL (for example `https://localhost:5001`). Every route starts with `/api/`.

**Role Required** key:

| Value | Meaning |
|---|---|
| None | Public — no login required |
| Any | Any authenticated user (Organiser or Participant) |
| Organiser | Organiser role only |
| Participant | Participant role only |

---

## 1. Authentication (register and login)

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Creates a new account and assigns the selected role (Organiser or Participant). Password is hashed before storage. | None | `{ firstName, lastName, email, password, role, dateOfBirth, phoneNumber }` | `201 Created` — new user with UserId and role. `400 Bad Request` — invalid or missing fields. `409 Conflict` — email already registered. |
| POST | `/api/auth/login` | Validates credentials and starts a server-side session that stores the user ID and role for subsequent requests. | None | `{ email, password }` | `200 OK` — session token plus user ID and role. `401 Unauthorized` — wrong email or password. |
| POST | `/api/auth/logout` | Ends the current session and marks it inactive. | Any | None | `200 OK` — session ended. `401 Unauthorized` — no active session. |

## 2. User Profile (view and update own profile)

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/users/me` | Returns the profile of the currently logged-in user. | Any | None | `200 OK` — user profile. `401 Unauthorized` — not logged in. |
| PUT | `/api/users/me` | Updates the profile details of the currently logged-in user. | Any | `{ firstName, lastName, email, phoneNumber, dateOfBirth }` | `200 OK` — updated profile. `400 Bad Request` — invalid values. `401 Unauthorized` — not logged in. `409 Conflict` — email used by another account. |

## 3. Events (Organisers manage; both roles view)

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events` | Returns all upcoming events, optionally filtered by type or location. | None | None | `200 OK` — list of events. |
| GET | `/api/events/{id}` | Returns the full details of a single event including its route information. | None | None | `200 OK` — event detail. `404 Not Found` — event does not exist. |
| POST | `/api/events` | Creates a new event owned by the logged-in Organiser. | Organiser | `{ name, description, date, location, distanceKm, eventType }` | `201 Created` — new event. `400 Bad Request` — invalid fields (distance ≤ 0, bad event type). `401 Unauthorized` — not logged in. `403 Forbidden` — Participant attempted to create an event. |
| PUT | `/api/events/{id}` | Updates an existing event. Only the Organiser who owns the event may update it. | Organiser | `{ name, description, date, location, distanceKm, eventType }` | `200 OK` — updated event. `404 Not Found` — event does not exist. `403 Forbidden` — Organiser does not own the event. |
| DELETE | `/api/events/{id}` | Deletes an event and its dependent enrolments/results. Owner only. | Organiser | None | `204 No Content` — event deleted. `404 Not Found` — event does not exist. `403 Forbidden` — Organiser does not own the event. |

## 4. Categories (Organisers define age/distance categories per event; both roles view)

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events/{id}/categories` | Lists all categories available for a specific event. | None | None | `200 OK` — category list. `404 Not Found` — event does not exist. |
| POST | `/api/events/{id}/categories` | Adds a category (e.g. Under 20, Senior, 10km, 21km) to an event. Owner Organiser only. | Organiser | `{ categoryName, minAge, maxAge }` | `201 Created` — new category. `400 Bad Request` — invalid age range or duplicate name. `403 Forbidden` — not the owning Organiser. `404 Not Found` — event does not exist. |
| PUT | `/api/categories/{id}` | Updates a category name or age range. | Organiser | `{ categoryName, minAge, maxAge }` | `200 OK` — updated category. `403 Forbidden` — not the owning Organiser. `404 Not Found` — category does not exist. |
| DELETE | `/api/categories/{id}` | Removes a category that has no enrolments yet. | Organiser | None | `204 No Content` — category removed. `409 Conflict` — category still has enrolments. `403 Forbidden` — not the owning Organiser. `404 Not Found` — category does not exist. |

## 5. Event Enrolments (Participants enter events; Organisers view enrolments)

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/events/{id}/enrolments` | Enters the logged-in Participant into an event using a selected category. Records the link between Participant, event and category. | Participant | `{ categoryId }` | `201 Created` — enrolment record with enrolment date. `400 Bad Request` — invalid category for the event. `401 Unauthorized` — not logged in. `403 Forbidden` — Organiser attempted to enrol. `404 Not Found` — event or category does not exist. `409 Conflict` — already enrolled in this event. |
| GET | `/api/events/{id}/enrolments` | Returns every enrolment for an event, used by the Organiser to manage race day. | Organiser | None | `200 OK` — list of enrolments with participant and category details. `403 Forbidden` — not the owning Organiser. `404 Not Found` — event does not exist. |
| GET | `/api/enrolments/me` | Returns all of the current Participant's own enrolments. | Participant | None | `200 OK` — list of the participant's enrolments. `401 Unauthorized` — not logged in. |
| DELETE | `/api/enrolments/{id}` | Cancels the logged-in Participant's own enrolment before the event. | Participant | None | `204 No Content` — enrolment cancelled. `403 Forbidden` — cancelling someone else's enrolment. `404 Not Found` — enrolment does not exist. |


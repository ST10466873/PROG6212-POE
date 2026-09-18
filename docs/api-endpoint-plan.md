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


# API Endpoint Plan

## Organisers

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/organisers/register` | Register a new organiser | None | `{ "name", "email", "password" }` | `201 Created` with new organiser ID |
| POST | `/api/organisers/login` | Login an organiser | None | `{ "email", "password" }` | `200 OK` with JWT token |

## Participants

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/participants/register` | Register a new participant | None | `{ "name", "email", "password", "age", "gender" }` | `201 Created` with new participant ID |
| POST | `/api/participants/login` | Login a participant | None | `{ "email", "password" }` | `200 OK` with JWT token |

## Events

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events` | Get a list of all upcoming events | None | None | `200 OK` with list of events |
| GET | `/api/events/{id}` | Get details of a specific event | None | None | `200 OK` with event details |
| POST | `/api/events` | Create a new event | Organiser | `{ "name", "eventDate", "location", "description" }` | `201 Created` with new event ID |
| PUT | `/api/events/{id}` | Update an existing event | Organiser | `{ "name", "eventDate", "location", "description" }` | `200 OK` or `204 No Content` |
| DELETE | `/api/events/{id}` | Delete an event | Organiser | None | `204 No Content` |

## Categories

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | `/api/events/{id}/categories` | Get categories for an event | None | None | `200 OK` with list of categories |
| POST | `/api/events/{id}/categories` | Add a category to an event | Organiser | `{ "categoryId" }` | `201 Created` |

## Enrolments

| Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | `/api/enrolments` | Enroll a participant in an event category | Participant | `{ "eventId", "categoryId" }` | `201 Created` with enrolment ID |
| GET | `/api/enrolments/participant/{id}` | Get enrolments for a participant | Participant | None | `200 OK` with list of enrolments |
| GET | `/api/enrolments/event/{id}` | Get enrolments for an event | Organiser | None | `200 OK` with list of enrolments |

| GET | /api/participants | Get all participants | Admin | None | 200 OK |
| DELETE | /api/participants/{id} | Delete a participant | Admin | None | 204 No Content |
| PUT | /api/participants/{id} | Update a participant | Participant | { data } | 200 OK |
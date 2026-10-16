# RaceDay REST API (Part 2)

ASP.NET Core 9 Web API for the RaceDay system from Part 1. The approved plan and ERD are kept in [`PROG A1/docs`](PROG%20A1/docs). Authentication uses server-side ASP.NET session state as required by Part 2; the Part 1 JWT note is superseded for this implementation.

## Roles

- **Organiser**: register and log in, update own profile, create and manage own events, define and assign categories, view event enrolments, and record results.
- **Participant**: register and log in, update own profile, browse events and categories, enrol in an available category, and view own enrolments and results.

Organiser and participant registration use separate endpoints, so a client cannot select a privileged role in the request body. Passwords are stored as ASP.NET password hashes. Session cookies are HttpOnly, SameSite Strict, and expire after 30 minutes of inactivity.

## Requirements

- .NET 9 SDK
- SQL Server or SQL Server LocalDB

## Configure and run

From this directory, set a SQL Server connection string using the `ConnectionStrings__RaceDay` environment variable, or edit `RaceDay.Api/appsettings.Development.json`. The default connection targets SQL Server LocalDB.

```powershell
dotnet tool install --global dotnet-ef --version 9.0.11
dotnet ef database update --project RaceDay.Api --startup-project RaceDay.Api
dotnet run --project RaceDay.Api
```

Swagger UI is available at `/swagger` in Development. Use the same browser session for login and subsequent role-protected calls so the session cookie is sent.

## Main endpoints

| Method | Route | Access | Purpose |
|---|---|---|---|
| POST | `/api/organisers/register` | Public | Create an organiser account |
| POST | `/api/organisers/login` | Public | Start an organiser session |
| POST | `/api/participants/register` | Public | Create a participant account |
| POST | `/api/participants/login` | Public | Start a participant session |
| POST | `/api/logout` | Signed in | End the current session |
| GET, PUT | `/api/profile` | Signed in | Read or update the current user's profile |
| GET | `/api/events`, `/api/events/{id}` | Public | Browse upcoming events or event details |
| POST, PUT, DELETE | `/api/events` and `/api/events/{id}` | Organiser | Manage events owned by the signed-in organiser |
| GET | `/api/events/{id}/categories` | Public | List categories offered for an event |
| POST | `/api/events/{id}/categories` | Owning organiser | Assign an existing category to an event |
| POST | `/api/events/{id}/categories/new` | Owning organiser | Create and assign a category |
| POST | `/api/enrolments` | Participant | Enrol in a category offered by an event |
| GET | `/api/enrolments/participant/{id}` | That participant | View own enrolments and results |
| GET | `/api/enrolments/event/{id}` | Owning organiser | View event enrolments |
| PUT | `/api/enrolments/{id}/result` | Owning organiser | Record a finish time and position |

## Database

Entity Framework Core code-first migrations are in `RaceDay.Api/Data/Migrations`. The six Part 1 entities and relationships are retained. Part 2 adds event distance/type and enrolment finish time/position so the required event and result workflows can be represented. Apply migrations with the command above.

## Tests and CI

Run locally with:

```powershell
dotnet test RaceDay.sln
```

GitHub Actions workflow: [`.github/workflows/ci.yml`](.github/workflows/ci.yml). It restores, builds, and runs the tests on pushes and pull requests. A successful run screenshot must be added after the workflow runs in GitHub; this workspace does not yet contain a verified GitHub Actions run.

## Submission items to finish

- Add the real green GitHub Actions run screenshot after pushing this project and waiting for CI to pass.
- Build up the required 20 meaningful commits through genuine development changes in your own GitHub repository; no commit history has been fabricated here.
- Replace the Part 1 placeholder YouTube link with the unlisted Part 2 demonstration video.
- Submit the repository link on ARC.

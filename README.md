# School Admission System

First-come, first-served school admission: admins create classes (each shift is its own class) with limited seats, guardians apply (temporary seat hold), then pay a simulated fee to confirm.

## Architecture

- **Backend** (`backend/SchoolAdmission.Api`): .NET 8 REST API, EF Core + PostgreSQL. `Controllers` (HTTP) -> `Services/AdmissionService` (business rules, transactions) -> `Data/AppDbContext`. A background worker marks expired holds every 5 seconds.
- **Frontend** (`frontend/`): React 18 + TypeScript + Vite, served by nginx. nginx proxies `/api/*` to the backend (`BACKEND_URL`), so the browser needs no CORS setup.
- **Database**: PostgreSQL 16 with a named Docker volume (`pgdata`).
- **Concurrency**: see `ASSUMPTIONS.md`.

## Run

Only Docker is needed.

```
docker compose up --build
```

- Frontend UI: http://localhost:3000
- Backend API: http://localhost:8080

Stop with `docker compose down` (data is kept). Do **not** add `-v` unless you want to delete the data.

## Environment variables

| Variable | Used by | Default |
|---|---|---|
| `DB_CONNECTION` | backend | set in docker-compose.yml from the POSTGRES_* values |
| `HOLD_DURATION_SECONDS` | backend | `600` |
| `BACKEND_URL` | frontend (nginx) | `http://backend:8080` |
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | db, backend | dev-only defaults; override with a `.env` file (see `.env.example`) |

Example (20 second holds): set `HOLD_DURATION_SECONDS=20` in `.env`, then `docker compose up -d`.

## API

Student identity is the `X-Student-Id` header (17-digit birth registration number).

| Method | Path | Notes |
|---|---|---|
| POST | /classes | `{"name":"Class 6 - Morning Shift","totalSeats":120}` -> 201 |
| GET | /classes | list with `heldSeats`, `confirmedSeats`, `availableSeats` |
| GET | /classes/{classId} | one class |
| POST | /classes/{classId}/holds | header required -> 201, 409 if no seats |
| GET | /holds/{holdId} | HELD / CONFIRMED / EXPIRED / CANCELLED |
| POST | /holds/{holdId}/confirm | header required, idempotent |
| DELETE | /holds/{holdId} | header required -> 204 |
| GET | /me/holds | header required |

Errors: 400 invalid input, 403 someone else's hold, 404 not found, 409 conflict (no seats, duplicate, expired).

```
curl -X POST http://localhost:8080/classes -H "Content-Type: application/json" -d "{\"name\":\"Class 6\",\"totalSeats\":2}"
curl -X POST http://localhost:8080/classes/<classId>/holds -H "X-Student-Id: 20152691234567890"
```

## Tests

Backend tests run against a real PostgreSQL inside Docker (no local .NET needed):

```
docker compose --profile test run --rm tests
```

They cover: successful hold, hold expiry and seat release, idempotent confirmation (including 20 parallel confirms), concurrent holds (60 simultaneous requests for 5 seats), duplicate student requests, and input validation.
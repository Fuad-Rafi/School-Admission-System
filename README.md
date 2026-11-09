# School Admission System

Online school admission on a **first-come-first-served** basis. Each class+shift has a fixed number
of seats. Applying places a time-limited **hold** on a seat; paying the admission fee inside that
window **confirms** it; otherwise the seat is released automatically.

**Stack:** .NET 8 · PostgreSQL 16 · React 18 + TypeScript (Vite) · Docker Compose

---

## Quick start

**Prerequisite:** Docker Desktop (with Compose v2). Nothing else needs to be installed.

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| Frontend (use this) | http://localhost:3000 |
| Backend API | http://localhost:8080 |
| PostgreSQL | internal only (`db:5432`) |

Data is stored in the named volume `pgdata`. `docker compose down` keeps it; `docker compose down -v`
deletes it (fresh start).

---

## Configuration

| Variable | Service | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings__Default` | backend | `Host=db;Port=5432;Database=admission;Username=postgres;Password=postgres` | Database connection |
| `HOLD_DURATION_SECONDS` | backend | `600` | Hold lifetime before automatic expiry |
| `HOLD_SWEEP_INTERVAL_SECONDS` | backend | `1` | Expiry sweeper cadence |
| `CORS_ORIGINS` | backend | `http://localhost:3000,http://localhost:5173` | Allowed browser origins |
| `API_BASE_URL` | frontend | `http://localhost:8080` | Backend URL, injected at container start (no rebuild) |

Example — shorten holds to 30 seconds:

```bash
HOLD_DURATION_SECONDS=30 docker compose up --build
```

PowerShell:

```powershell
$env:HOLD_DURATION_SECONDS = "30"; docker compose up --build
```

---

## How it works

- **Every class+shift is a class** (`Class 6 - Morning Shift` = one class, `Class 6 - Day Shift` =
  another), each with its own seat count.
- **Identity:** the student's 17-digit birth-registration number, sent as `X-Student-Id`. No login.
  The UI stores it in `localStorage`, so refresh/browser restart keeps the session.
- **Apply** → `HELD` with `expiresAt = now() + HOLD_DURATION_SECONDS`.
- **Pay admission fee** → `CONFIRMED` (payment is simulated and always succeeds).
- **Cancel / expiry** → the seat returns to the pool automatically. No client call is needed for
  expiry.
- **One active hold or confirmed admission per student per class.**
- **No overselling:** seat allocation is serialised with a PostgreSQL row lock on the class, so it is
  correct under concurrent requests and across multiple backend processes.

---

## Project structure

```
.
├── backend/
│   ├── Admission.Api/            # ASP.NET Core 8 Web API
│   │   ├── Contracts/            # API DTOs
│   │   ├── Data/                 # DbContext, entities, migrations
│   │   ├── Endpoints/            # /classes, /holds, /me/holds
│   │   ├── Services/             # seat allocation, expiry sweeper
│   │   └── Middleware/           # error mapping
│   ├── Admission.Api.Tests/      # xUnit + Testcontainers (incl. concurrency test)
│   └── Dockerfile
├── frontend/                     # React 18 + TypeScript + Vite
│   ├── src/
│   ├── Dockerfile
│   └── nginx.conf
├── scripts/
│   └── multi-process-check.mjs   # two-host overbooking check (bonus)
├── docker-compose.yml
├── .env.example
├── README.md
├── ASSUMPTIONS.md
└── AI_USAGE.md
```

---

## API

Base URL `http://localhost:8080`; JSON everywhere.

| # | Method & path | Purpose | `X-Student-Id` |
|---|---|---|---|
| 1 | `POST /classes` | Create class | – |
| 2 | `GET /classes` | List classes with seat counts | – |
| 3 | `GET /classes/{classId}` | One class | – |
| 4 | `POST /classes/{classId}/holds` | Apply (hold a seat) | ✅ |
| 5 | `GET /holds/{holdId}` | Hold status | – |
| 6 | `POST /holds/{holdId}/confirm` | Pay / confirm | ✅ |
| 7 | `DELETE /holds/{holdId}` | Cancel | ✅ |
| 8 | `GET /me/holds` | My applications | ✅ |

Statuses: `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`. Errors use
`{ "code": "...", "message": "..." }`; `message` is safe to display to users.

---

## Running the tests

### Backend (requires Docker for Testcontainers)

```bash
dotnet test backend/Admission.Api.Tests/Admission.Api.Tests.csproj
```

Without Docker-in-Docker, point the tests at an existing database:

```bash
TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=admission_test;Username=postgres;Password=postgres" \
  dotnet test backend/Admission.Api.Tests/Admission.Api.Tests.csproj
```

### Frontend

```bash
cd frontend
npm ci
npm test
```

The completed project has **43 backend test cases** and **7 frontend test cases**, all passing. The
backend suite includes a **concurrency test**: many simultaneous applications for a class with
fewer seats must never produce more holds than seats.

---

## Bonus: two backend processes, one database

```bash
docker compose --profile multi up -d --build
node scripts/multi-process-check.mjs http://localhost:8080 http://localhost:8081 40 200
```

This starts the normal backend on `:8080` and a second instance on `:8081`, both using the same
PostgreSQL database. Seat integrity is enforced in the database, so concurrent traffic through both
ports still never oversells (the check must print `"errors": []`). Each process caps its connection
pool at 40 so two processes fit inside PostgreSQL's default 100-client limit. See `ASSUMPTIONS.md`
for the reasoning.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Port 8080 already in use | Stop the other process/container, or change the host mapping |
| Port 3000 already in use | Change `3000:80` to another host port in `docker-compose.yml` |
| Stale data after changing the schema | `docker compose down -v` (destroys data) then `up --build` |
| CORS error in the browser | Check `CORS_ORIGINS` includes the frontend origin |
| Frontend cannot reach API | Check `API_BASE_URL` and that the backend is healthy at `/health` |
| Backend exits on start | Database not ready; check the `db` healthcheck and backend logs |

---

## Assumptions & AI usage

- `ASSUMPTIONS.md` — decisions where the brief is silent.
- `AI_USAGE.md` — AI tools used, where they helped, and what was caught.

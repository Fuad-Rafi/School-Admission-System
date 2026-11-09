# AI_USAGE

## 1. AI Tools Used

- **OpenCode coding agent (DeepSeek V4.1 Flash):** architecture discussions, code scaffolding,
  debugging concurrency and Docker issues, test generation, and documentation drafting.

AI was used as an assistant only. I reviewed and modified generated code before integrating it and
did not treat AI output as automatically correct.

## 2. Where I Used AI

| Project Area | How AI Assisted |
|---|---|
| Backend (.NET) | Project structure, DTOs, API endpoints, service-layer patterns, error middleware, and configuration. |
| Database | EF Core entities, DbContext configuration, migration, indexes, and check constraints. |
| Seat Reservation | Transaction boundaries, row locking, idempotent confirmation, cancellation, and expiry. |
| Frontend (React) | Components, polling/countdown hooks, API client, form validation, and UI polish. |
| Docker | Backend/frontend Dockerfiles, Compose services, health checks, profiles, and runtime frontend configuration. |
| Testing | Testcontainers, xUnit, concurrency/expiry/persistence/multi-instance tests, Vitest, and MSW. |
| Documentation | README, API contract, architecture notes, assumptions, and this AI usage summary. |

## 3. An AI Mistake I Identified

**Problem: Multi-process database connection exhaustion**

The seat logic correctly used a PostgreSQL row lock to prevent overselling, but the AI-assisted
setup left each backend process on the default Npgsql pool size of 100. PostgreSQL's default
`max_connections` is also 100.

When I started two backend processes against the same database, the load check sent 200 concurrent
applications for a class with 40 seats. Capacity stayed correct, but 95 requests returned HTTP 500.
Logs showed:

```text
Npgsql.PostgresException 53300: sorry, too many clients already
```

The AI had focused on row-lock correctness and did not consider connection-pool sizing across
processes.

**How I fixed it:**

I capped each backend at 40 connections:

```text
Maximum Pool Size=40
```

Two processes now use at most 80 of PostgreSQL's 100 clients.

I re-ran the same check across `localhost:8080` and `localhost:8081`:

- 40 seats
- 200 simultaneous applications
- exactly 40 × HTTP 201
- 160 × HTTP 409
- zero HTTP 5xx
- both processes reported `heldSeats = 40`, `availableSeats = 0`


## 4. Verification

- Backend tests: **43/43 passing**, including concurrency, expiry, persistence, and multi-instance tests.
- Frontend tests: **7/7 passing**.
- Frontend production build: successful.
- Concurrent seat reservations: no overbooking.
- Expired holds release seats automatically.
- Data survives `docker compose down` and `up`.
- Full stack starts with `docker compose up --build`.
- Two backend processes on one database never oversell.
- AI-generated code was manually reviewed before integration.

AI helped accelerate development, but I was responsible for understanding, testing, and
validating the final implementation.

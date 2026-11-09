# ASSUMPTIONS

Where the brief is silent, these are the assumptions we make and why.

## Product assumptions

1. **"Class" means "class + shift".** Every shift is a separate class row with its own seat count
   (e.g. `Class 6 - Morning Shift` = 120 seats, `Class 6 - Day Shift` = 80 seats). The brief states
   this explicitly (section 3); names are free text, so no grade/shift columns are modelled.
2. **First-come-first-served.** Order is determined by the moment the hold is created in the
   database. There is no lottery, waitlist or priority.
3. **Identity is self-asserted.** The 17-digit birth-registration number in `X-Student-Id` is the
   only identity. There is no authentication; anyone can act as any student, and there is no admin
   login. This matches the brief ("Login লাগবে না") and keeps scope controlled.
4. **A student may hold/confirm seats in several *different* classes,** but at most one active
   (`HELD`) hold **or** confirmed admission **per class** at a time. Cancelling/expiring a hold
   allows re-applying to the same class.
5. **Payment always succeeds.** "Pay admission fee" is a simulated, instant confirmation; there is
   no gateway, receipt, refund or payment state.
6. **Cancel applies to active holds only.** `DELETE /holds/{id}` cancels a `HELD` hold. A
   `CONFIRMED` admission cannot be cancelled/withdrawn with this API (the brief's UI only offers
   "Cancel" on an application awaiting payment). Repeated cancel of an already `CANCELLED` hold is
   treated as idempotent success (204).
7. **Expiry is terminal.** An `EXPIRED` or `CANCELLED` hold never becomes `HELD` again; the student
   creates a new hold.
8. **`expiresAt` is always returned in UTC ISO-8601** (e.g. `2026-11-01T04:10:00Z`), including for
   terminal states, where it records the original deadline.
9. **`heldSeats` only counts non-expired `HELD` holds,** even if the background sweeper has not yet
   flipped the row to `EXPIRED`. Therefore `heldSeats + confirmedSeats <= totalSeats` always holds
   and `availableSeats = totalSeats - heldSeats - confirmedSeats`.
10. **Duplicate class names are allowed** (the brief does not require uniqueness). The UI shows
    them independently.
11. **No pagination.** Class and hold lists are returned in full; expected volume in a demo is
    small. Ordering is deterministic (classes: oldest first; my holds: newest first).

## Technical assumptions

12. **PostgreSQL is the source of truth for time.** Expiry uses the database clock (`now()`) in all
    comparisons and in `expiresAt`, so two backend processes/containers cannot disagree due to
    clock skew. Application servers do not decide expiry with their own clocks.
13. **Seat integrity is enforced by the database, not by process memory.** All seat-changing
    operations run in a `SERIALIZABLE`-equivalent critical section: a `SELECT ... FOR UPDATE` row
    lock on the class row, followed by a count of active holds and the insert/update, inside one
    transaction. No in-process semaphores/locks are used for correctness, which is what makes the
    multi-process bonus requirement safe.
14. **A partial unique index is the last line of defence** for "one active hold/confirmed
    admission per student per class": `UNIQUE (class_id, student_id) WHERE status IN
    ('HELD','CONFIRMED')`.
15. **Expired holds are persisted as `EXPIRED` by a background sweeper** (runs every
    `HOLD_SWEEP_INTERVAL_SECONDS`, default 1s) so that no API call is needed to release a seat.
    Reads additionally project an overdue `HELD` as `EXPIRED` as a defensive measure; both
    mechanisms are idempotent.
16. **`HOLD_DURATION_SECONDS` is honoured exactly** and defaults to `600` when unset or invalid.
    It is read at startup. A value `<= 0` is rejected/replaced by the default.
17. **IDs are opaque strings** (UUIDv4 rendered in canonical form). The example IDs `c_123` and
    `h_789` in the brief are illustrative; the brief explicitly allows any format.
18. **Error bodies are our own shape** (`{ "code", "message" }`) because the brief does not define
    error payloads; only success payload fields are contractual. Human-readable `message` is what
    the UI displays.
19. **Extra fields may be added to responses** (allowed by the brief) but no required field is
    removed or renamed. We keep extras minimal (e.g. `serverTime` where useful).
20. **HTTP status choices not fixed by the brief:** 403 when acting on another student's hold, 409
    for state conflicts (full class, duplicate active hold, expired/cancelled confirm), 404 for
    unknown class/hold, 400 for malformed/missing `X-Student-Id` or body.
21. **Frontend configuration is injected at container start,** not baked at build time, so the
    backend URL is genuinely changeable via the `API_BASE_URL` environment variable without a
    rebuild (Vite normally inlines `VITE_*` at build time — see ADR-010).
22. **The UI keeps the student ID in `localStorage`,** so a refresh or browser restart restores the
    "my applications" view.
23. **Docker Compose is the only supported runtime.** The reviewer needs nothing installed except
    Docker. A named volume backs PostgreSQL and survives `docker compose down` / `up`.
24. **Tests may use a throw-away database.** Testcontainers starts PostgreSQL for the automated
    suite; a `TEST_DB_CONNECTION` fallback supports environments without Docker-in-Docker.

## Multi-process bonus (ADR-003, ADR-004)

25. Two backend processes on different ports can share one database and still never oversell,
    because capacity checks are serialised by a PostgreSQL row lock and expiry uses `now()` from
    the shared database. A second instance is started with
    `docker compose --profile multi up -d --build` (backend-2 on `:8081`).
26. **Each backend caps its Npgsql pool at 40 connections** (`Maximum Pool Size=40`) so two
    processes use at most 80 of PostgreSQL's default 100 connections. Without this cap, the
    two-host load check returned HTTP 500 (`53300: sorry, too many clients already`) even though
    occupancy stayed correct. The verified bonus run is 40 seats with 200 simultaneous applies
    split across both ports: exactly 40 × 201, 160 × 409, zero 5xx, and both processes report
    `heldSeats=40, availableSeats=0`. Run `node scripts/multi-process-check.mjs` to reproduce.

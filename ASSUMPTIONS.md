# ASSUMPTIONS

## Product / API

1. A student may apply to multiple different classes. The "one active or confirmed hold" rule applies per class.
2. Only a `HELD` application can be cancelled. `CONFIRMED` and `EXPIRED` cannot. Re-cancelling an already `CANCELLED` hold returns 204.
3. `EXPIRED` and `CANCELLED` are terminal. Re-applying creates a new hold.
4. Terminal holds keep their original `expiresAt`.
5. `heldSeats` counts only non-expired `HELD` holds. `availableSeats = totalSeats - heldSeats - confirmedSeats`.
6. Duplicate class names are allowed.
7. Lists are not paginated. Classes are ordered oldest first; "my holds" newest first.
8. Error bodies use `{ "code": "...", "message": "..." }`. The brief fixes 409 for no seats; we use 403/404/409/400 for the other cases we defined.
9. Polling is every 3 seconds for classes and 2 seconds for my applications, with a 1-second countdown.
10. An invalid or missing `HOLD_DURATION_SECONDS` falls back to 600.

## Concurrency and operations

11. The database clock (`now()`) is authoritative for expiry.
12. Occupancy is derived from `holds`; there are no materialised seat counters.
13. Apply uses one transaction: lock the class row with `SELECT ... FOR UPDATE`, expire overdue holds, check capacity/duplicates, then insert.
14. A partial unique index enforces one active/confirmed hold per `(class_id, student_id)`.
15. A background sweeper persists `HELD -> EXPIRED`; reads also project overdue `HELD` as `EXPIRED`.
16. PostgreSQL data lives in the named volume `pgdata`. `docker compose down` keeps it; `docker compose down -v` resets it.

## Multi-process

17. Two backend processes share one database. Seat correctness is enforced by the database row lock and partial unique index, not by process memory. Each process caps its pool at 40, so two processes use at most 80 of PostgreSQL's 100 connections. The load-test evidence is in `AI_USAGE.md`.

## Out of scope

- Admin authentication, class editing/deletion, notifications, and a real payment gateway.

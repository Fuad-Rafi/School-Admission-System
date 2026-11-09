# AI_USAGE

Short, honest log of where AI helped, where it was wrong, and how we caught it.

## Tools used

| Tool | Model / version | Where used |
|---|---|---|
| OpenCode (CLI coding agent) | `deepseek/deepseek-flash` (DeepSeek V4.1 Flash) | Reading the brief, structuring requirements, drafting architecture/API/UI/test/plan documents |
| _(to be updated)_ | _(e.g. IDE autocomplete, chat assistant)_ | _(implementation, tests, debugging)_ |

No AI tool is used at runtime by the application itself.

## Log

### 2026-11-08 — Brief extraction and project documentation

- **What AI did:** extracted the text of the confidential assignment PDF, translated/summarised the
  Bengali requirements into an English requirements catalogue, and drafted the API contract,
  architecture, UI spec, test plan and delivery plan.
- **Where the AI was wrong:** the first extraction attempt (pypdf) returned the Bengali glyphs as
  `"?"` and duplicated content, because the embedded font has no usable Unicode mapping. Trusting it
  would have silently dropped the entire Bengali specification.
- **How it was caught:** page text was cross-checked against a second extractor (PyMuPDF) and the
  rendered page images. The Bengali text appeared correctly in the second extraction, and the
  requirements were re-derived from that. Lesson applied: verify extracted specs against an
  independent representation before designing.
- **What AI did not decide alone:** all ambiguous points were turned into explicit assumptions in
  `09-assumptions.md` and ADRs in `07-decisions.md`, so a human can review each choice.

### 2026-11-08 — Phase 1 implementation (foundation)

- **What AI did:** scaffolded the ASP.NET Core 8 project, config/options/health endpoint, EF Core
  entities/DbContext, initial migration, startup migration retry, backend Dockerfile, and the
  Compose database/backend stack.
- **Where the AI was wrong:** while preparing the hostile low-memory Docker environment, its first
  attempt to lower the Docker Desktop VM memory rewrote `settings-store.json` with a UTF-8 BOM.
  Docker's JSON parser rejected the file (`invalid character 'A_'`) and the backend crashed.
- **How it was caught:** the error was in `com.docker.backend.exe.log`; the file was rewritten with
  `UTF8Encoding(false)` and the engine started. This was an environment fix, not application code.
- **Also caught:** `dotnet ef migrations add --no-build` failed because only a Release build existed;
  reran without `--no-build`. Generated SQL, tables, checks and partial indexes were reviewed
  independently, not accepted blindly.
- **Verified independently:** `dotnet build` (0 warnings/errors), Compose health, `/health` 200,
  schema and index inspection, and persistence across `docker compose down`/`up`.

### Implementation (continuing)

| Date | Area | Usage | What AI got wrong | How it was caught |
|---|---|---|---|---|
| 2026-11-08 | Backend foundation | Project scaffold, EF model/migration, Dockerfile, Compose | PowerShell/BOM issue in Docker settings (environment, not repo code) | Docker backend log; fixed encoding |
| 2026-11-08 | Backend API | DTOs, endpoints, row-lock seat service, expiry sweeper, error middleware | Missing `Microsoft.EntityFrameworkCore.Storage` using for `GetDbTransaction`; malformed JSON initially returned an empty 400 | Compiler error; smoke test → enabled `RouteHandlerOptions.ThrowOnBadRequest` |
| 2026-11-08 | Verification | Contract smoke, 40-way concurrency, idempotent confirm, expiry, persistence | Script helper sent a content body with GET (PowerShell), not app code | Failure reproduced and fixed in the test script |
| 2026-11-08 | Backend tests | xUnit + Testcontainers harness, 43 tests across contract/lifecycle/concurrency/expiry/persistence/multi-instance | First harness set the DB via `ConfigureAppConfiguration`, which is applied after the app reads its connection string, so all tests hit `127.0.0.1:5432`; a third test host then hit PostgreSQL `53300 too many clients`; EF package versions conflicted | Root-caused from the full-suite failures; switched to environment variables set before host creation, capped `MaxPoolSize=20`, aligned EF 8.0.33 packages → 43/43 green, 0 warnings |
| 2026-11-08 | Frontend tests | Vite/React UI, runtime config, 7 Vitest + RTL + MSW tests | jsdom had no `fetch`, so every test showed a network error; the apply test mocked an active hold before clicking Apply, disabling the button | Added an `undici` fetch polyfill; removed the premature hold mock → 7/7 green |
| 2026-11-08 | Phase 5 multi-process | Added `backend-2` profile + `scripts/multi-process-check.mjs`; verified two hosts on one DB | First run returned 95 × HTTP 500 because two default 100-connection pools exhausted PostgreSQL's 100-client limit (`53300`); AI initially missed connection-pool sizing | Caught by the check script and backend logs; capped each process at `Maximum Pool Size=40`; rerun produced exactly 40×201 / 160×409 with no 5xx |
| 2026-11-08 | Phase 5 UI QA | Browser-driven QA of identity, forms, apply, cancel, full class, restart banner, two tabs, expiry | Native `min=1` form validation blocked the React error message for 0 seats, so the styled error never appeared | Found during manual UI QA; added `noValidate` so all validation is handled consistently by React |

## Rules followed

- AI-generated code is reviewed against the written contract and covered by automated tests before
  being accepted.
- The concurrency-critical seat allocation logic is reviewed line by line and proven by a dedicated
  concurrent test, never accepted on AI output alone.
- Secrets, the confidential PDF and personal data are not pasted into AI tools.

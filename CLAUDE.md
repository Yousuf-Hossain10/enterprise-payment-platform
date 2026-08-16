# Enterprise Payment Platform — Capstone Project Context

This is a solo capstone project simulating a production-grade fintech platform (.NET 8 microservices, Angular, Kubernetes, full observability/security/CI-CD).

**Remote repository:** https://github.com/Yousuf-Hossain10/enterprise-payment-platform

Read the following files in this folder before doing any work — they are the full spec, in order of authority:

1. `Enterprise_Payment_Platform_Developer_Instruction.md` — original spec and non-negotiable engineering rules
2. `Enterprise_Payment_Platform_Tutorial.md` — the phase-by-phase build guide (19 phases, with code patterns and Definitions of Done)
3. `Phase4-17_Breakdown_and_Security_Model.md` — expanded phase detail and the Security-Model.md outline
4. `Sprint-Plan-Day-by-Day.md` — the day-by-day schedule this project follows; **this is what tells you what to build today**
5. `ADR-Template-and-Starter-Log.md` — Architecture Decision Record format and the log of decisions to make
6. `Learning-Journal-Template.md` — devlog format for documenting the learning journey
7. `Concept-Study-Guide.md` — underlying theory per phase

## Non-negotiable rules

- **One day's tasks at a time.** Look up today's day number in `Sprint-Plan-Day-by-Day.md` (see "Current Progress" below), do only that day's tasks, then stop for that session.
- **Always state which Day/Phase you're working on** before starting, and confirm it matches what's expected next.
- **Autonomy within a phase, checkpoint between phases.** You do not need my explicit approval for each day's local commit — commit to `main` at the end of each day's tasks with the message from `Sprint-Plan-Day-by-Day.md` (or an accurate equivalent if the work diverged), update "Current Progress" below, and move to the next day in the same session if I've asked you to keep going. Stop and wait for explicit review before: (1) pushing to `origin` — batch several days of local commits into one push when I ask for a checkpoint, not after every commit; (2) starting a new phase; (3) any ADR decision point (see below). If anything in a day's work feels architecturally significant or uncertain even if it's not an ADR point, flag it in your summary rather than pushing ahead silently.
- **Commit directly to `main` through Phase 13.** No feature branches or PRs yet — there's no CI to gate them on until Phase 14 adds branch protection and required checks (Day 71), so a PR before then has nothing to review against. Don't use `/create-pr` or similar until Phase 14.
- **Never skip ahead to a later phase** even if it would be more convenient — dependencies matter, and so does not overwhelming a single session.
- **No placeholder or stub code** unless a task explicitly calls for scaffolding — this repo is meant to read as production-oriented throughout, per the main instruction doc.
- **Follow the Definition of Done** for the current phase (in the tutorial) before considering a phase complete, not just the day's task list.
- **Commit messages**: Conventional Commits (`type(scope): imperative summary`), per the conventions section at the top of `Sprint-Plan-Day-by-Day.md`. Use the exact commit message suggested for the day unless the actual work diverged — then write an accurate one in the same style.
- **ADRs are mine to write, not yours.** When a day's tasks reach a decision point listed in `ADR-Template-and-Starter-Log.md`, flag it and stop — I write the reasoning myself. You may draft the "Options Considered" table if asked, but not the Decision or Trade-off Analysis sections.
- **Remind me to write a Learning Journal entry** at the end of any day marked with one in the sprint plan, before moving on.

## Current Progress

**Day:** 19
**Phase:** 5 — Identity Service
**Last completed:** Day 18 — Register/login endpoints with real Argon2id hashing (`Konscious.Security.Cryptography`, tuned parameters stored alongside the hash so future tuning doesn't invalidate old hashes, constant-time comparison on verify). `POST /api/v1/auth/register` and `/login` on a new `AuthController`; login and registration share the same generic error message for "unknown email" vs "wrong password" to avoid user enumeration. Wired `BuildingBlocks.Common` (correlation-ID, Problem Details) and `BuildingBlocks.Observability` (Serilog, OTel, health checks) into `Identity.Api` for the first time. **Real bug caught by actually running it, not just building it:** `Npgsql.OpenTelemetry` (from Day 15) resolves to Npgsql 10.x by default, binary-incompatible with the EF Core Npgsql provider's 8.x line — `Ping.Api`'s composition proof never caught this since it used in-memory fakes, not a real DbContext. Pinned `Npgsql.OpenTelemetry` and `AspNetCore.HealthChecks.NpgSql` to their `8.0.x` lines in `BuildingBlocks.Observability`/`Identity.Api`; documented as a platform-wide gotcha in `docs/Deployment-Strategy.md` since every future Postgres-touching service will hit it otherwise. Verified end-to-end for real: ran `Identity.Api`, curled register (201) → duplicate register (409) → invalid input (409 with both validation messages) → login (200) → wrong password (401) → unknown user (401, identical message) → `/health/ready` (200, real Postgres check) → independently confirmed the persisted row via `psql` (real Argon2id hash format). 14 new unit tests (fakes only, no DB — Testcontainers integration tests are Day 21). Full solution builds clean. Committed locally to `main`, not yet pushed.

*(Update this section at the end of every session so the next session picks up in the right place.)*

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

**Day:** 18
**Phase:** 5 — Identity Service
**Last completed:** Day 17 — `Identity.Domain`: `User` (email, password hash, `string[]` roles) and `RefreshToken` (token hash, expiry, revoked flag, `ReplacedByTokenHash` for rotation chaining), zero framework dependencies. `Identity.Infrastructure`: `IdentityDbContext` + EF Core configurations (unique index on `User.Email`, unique index on `RefreshToken.TokenHash`, FK with cascade delete, no navigation property on `User` by design) + a design-time factory for `dotnet ef` tooling (connection string overridable via `IDENTITY_DB_CONNECTION`, defaults to the standard port-forward). **Deviation caught early:** I was asked to use "mssql" for migrations; flagged that this contradicts `Technology-Decisions.md`'s committed Postgres decision before proceeding — confirmed staying on Postgres. Created a real `identity` database on the already-provisioned Postgres instance (owned by `payment_platform`), generated the `InitialCreate` migration, and **applied it for real** — verified via `kubectl exec` + `psql` that both tables, indexes, and the FK constraint exist exactly as configured. Wired `IdentityDbContext` into `Identity.Api`'s DI (endpoints come Day 18), hit and fixed a real EF Core package version mismatch between `Identity.Api` and `Identity.Infrastructure` (aligned both on Npgsql 8.0.11). Ran `Identity.Api` for real to confirm it boots with the DbContext registered. Made the database creation reproducible: added an idempotent `CREATE DATABASE identity OWNER payment_platform` step to `scripts/bootstrap.sh`/`.ps1` (checked via `SELECT 1 FROM pg_database`), verified idempotent on a real re-run. Documented the new database in `docs/Deployment-Strategy.md`. Full solution builds clean. Committed locally to `main`, not yet pushed.

*(Update this section at the end of every session so the next session picks up in the right place.)*

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

**Day:** 21
**Phase:** 5 — Identity Service
**Last completed:** Day 20 — Refresh token rotation + revocation. `POST /api/v1/auth/refresh` (rotates: revokes the presented token, chains it to its replacement via `ReplacedByTokenHash`, issues a new pair) and `POST /api/v1/auth/logout` (explicit revocation, independent of rotation). Refactored `IRefreshTokenRepository` from an auto-saving `AddAsync` to split `Add`(sync, stages only)/`SaveChangesAsync` so revoke-old + add-new happen in one atomic transaction — with two separate saves, a failure between them could revoke a token without ever persisting its replacement, locking the caller out. Added `IUserRepository.GetByIdAsync` (refresh has no email, only the token's stored `UserId`). Verified end-to-end for real against the live Postgres instance, including a mistake worth noting: my first logout test accidentally reused a stale already-rotated token from an earlier shell variable (shell state doesn't persist across separate tool calls) — caught it, redid the test cleanly against a genuinely active token, confirmed real 204 → subsequent-refresh-401 behavior. Also confirmed reuse of a rotated-away token is rejected (401) and the rotation chain is visible in the database via `psql`. 6 new tests (rotation success, reuse-of-revoked-token rejection, expired-token rejection, logout success/failure) plus the Day 19 login tests updated for the repository shape change — 25 total in `Identity.Tests`. Full solution builds clean. Committed locally to `main`, not yet pushed.

*(Update this section at the end of every session so the next session picks up in the right place.)*

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
- **ADRs are drafted by Claude, reviewed by me.** (Reversed 2026-08-17 — originally "mine to write, not yours.") When a day's tasks reach a decision point listed in `ADR-Template-and-Starter-Log.md`, draft the full ADR — Context, Decision, Options Considered, Trade-off Analysis, Consequences, Action Items — grounded in the actual implementation (real file paths, real parameters), and flag it for review rather than stopping and waiting.
- **Remind me to write a Learning Journal entry** at the end of any day marked with one in the sprint plan, before moving on.

## Current Progress

**Day:** 33
**Phase:** 7 — Payment Service (Phase 6 — Wallet Service — complete)
**Last completed:** Day 33 — `CapturePaymentAsync` orchestration, the saga itself. `CapturePaymentCommandHandler` (`Payment.Application`) deliberately matches the tutorial's snippet exactly, including *not* saving the in-memory `MarkAuthorized()` transition on its own — only the terminal outcome (`Failed`/`Captured`) is ever persisted, so a crash between authorization and the Wallet call returning leaves the stored payment at `Created`, letting a retried capture start clean instead of getting stuck in a half-persisted `Authorized` limbo. Proved this crash-safety property holds for real against Postgres, not just asserted in a docstring. `IPaymentRepository`/`PaymentRepository` mirror Wallet's pattern; `PaymentDbContext`/`PaymentConfiguration`/migration are new — first Payment work needing real persistence (Day 31 was Domain-only, Day 32 needed no DB). `Status` stored as text via `HasConversion<string>()`. `IdempotencyKey` has a unique index, proved via a dedicated integration test. `InitialCreate` migration applied to the real cluster Postgres; `payment` database added to bootstrap scripts. `Payment.Api/Program.cs` now wires DbContext/repository/handler, brought up to the same Serilog/Observability/health-check baseline as Identity.Api/Wallet.Api — verified by actually running it against the live cluster with a passing readiness check. No HTTP endpoint for triggering a capture yet (Day 34 adds `POST /payments` with idempotency enforcement). 11 new tests (7 unit + 4 real-Postgres integration). Full `Payment.Tests` suite: 50/50 passing. Full solution builds clean. Committed to `main` (`feat(payment): implement capture orchestration saga`), not yet pushed.

**Backlog note:** empty ADR skeletons (0008-0019) and Learning Journal entry skeletons (all 14 sprint-plan-marked days) were backfilled into `docs/adr/` and `docs/journal/` per explicit request — scaffolding only, not drafted content; a Phase 5/6 Learning Journal entry is still outstanding for the user to actually write.

*(Update this section at the end of every session so the next session picks up in the right place.)*

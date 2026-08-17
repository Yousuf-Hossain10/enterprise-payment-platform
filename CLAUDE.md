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

**Day:** 26
**Phase:** 6 — Wallet Service (Phase 5 — Identity Service — complete)
**Last completed:** Day 26 — outbox integration for `WalletDebited`/`WalletCredited`. `WalletDebited`/`WalletCredited` (`Wallet.Domain`) are enqueued in the *same* `SaveChangesAsync` transaction as the `LedgerEntry` insert they describe, via a new `IAccountRepository.EnqueueEvent(type, payload)` — implemented in `AccountRepository` (Infrastructure) with `System.Text.Json`, keeping `Wallet.Application` free of any `BuildingBlocks.Messaging`/EF dependency. `LedgerWriter.ApplyAsync` calls it alongside the ledger write, so both Debit and Credit get it for free. `WalletOutboxStore` implements the Phase-4 `IOutboxStore` against `WalletDbContext` for `OutboxDispatcherBackgroundService` to poll. `Wallet.Api/Program.cs` now wires `WalletDbContext`, the Debit/Credit handlers, and `AddOutboxDispatcher()`/`WalletOutboxStore` — still no HTTP endpoints (per Day 24's deferred service-to-service-auth decision), but the dispatcher itself runs regardless, since it only depends on what's already durably committed. New `AddOutboxMessages` migration applied to the real cluster Postgres. **Verified for real, end-to-end:** ran `Wallet.Api` live against the cluster's actual Postgres and RabbitMQ (port-forwarded), manually inserted an outbox row, and watched the running dispatcher pick it up, publish it (the `payment-platform.events` topic exchange was declared on RabbitMQ), and mark it processed — confirmed via both the API's structured logs and `psql`. 10 new/updated tests (unit: both handlers now assert `EnqueueEvent` is called correctly; integration: real Testcontainers Postgres proving the outbox row commits atomically with the ledger write, idempotent replay doesn't double-enqueue, and `WalletOutboxStore`'s read/mark-processed cycle works for real). Full `Wallet.Tests` suite: 26/26 passing. Full solution builds clean. Committed to `main` (`feat(wallet): publish ledger events via outbox pattern`), not yet pushed.

**Session note:** this session picked up after another interruption — Docker Desktop and the K8s cluster were both down at session start and needed ~10-15 minutes to come back up before Postgres/RabbitMQ port-forwards worked; no code was lost, Day 25 was already complete and just needed re-verification (done, then pushed) before Day 26 began.

**ADR note:** ADR authorship was reversed in a prior session (2026-08-17) per explicit direction — Claude now drafts full ADRs (Context/Decision/Options/Trade-offs/Consequences), user reviews/accepts. ADR-0001 through ADR-0005 are all written and Accepted in `docs/adr/` (`docs/Technology-Decisions.md`'s index reflects this). A Phase 5 Learning Journal entry is still outstanding — worth writing before too much Phase 6 context displaces it.

**Infra note (not part of Phase 5 scope, carried from the prior session):** Docker Desktop was reinstalled fresh (v29.7.2, WSL2 backend) with data at `D:\Docker_Data\DockerDesktopWSL`; NuGet's global cache lives at `D:\MyCash\.nuget\packages`. Single-node Kubernetes (7168 MiB RAM, 6 CPUs) is the proven-stable config after a 3-node attempt failed on genuine resource starvation. `docs/Deployment-Strategy.md` still needs an update reflecting the Hyper-V→WSL2 backend change and new data location — not yet done, tracked as documentation debt. An orphaned ~3.47GB file at `C:\ProgramData\DockerDesktop\vm-data\DockerDesktop.vhdx` needs manual elevated cleanup by the user, not urgent. `kube-prometheus-stack` Grafana was crash-looping from resource contention as of the last check — should be reconfirmed healthy before considering observability fully verified again, but does not block Identity/Phase 5 work.

*(Update this section at the end of every session so the next session picks up in the right place.)*

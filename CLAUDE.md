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

**Day:** 29
**Phase:** 6 — Wallet Service (Phase 5 — Identity Service — complete)
**Last completed:** Day 29 — ledger reconciliation report. Wallet has no cached `Balance` column by design, so there's nothing for a ledger sum to drift from in the classic "cached value vs. source of truth" sense. What this reconciles instead: two independent computation paths for the same figure. `IReconciliationRepository.GetAccountLedgerSumsAsync` (Infrastructure) returns both the normal LINQ-translated sum (same path `GetBalanceAsync` uses) and a hand-written raw SQL sum as ground truth — EF Core 8's `SqlQuery<T>` requires the single result column aliased exactly `"Value"` (case-sensitive; Postgres folds unquoted identifiers to lowercase), which cost one failed test run to discover and fix. If the two paths ever disagree, that's a P1 bug in the ORM translation or schema, not a warning, per the tutorial's own framing. `GetReconciliationReportQueryHandler` (`Wallet.Application`, verb-first per Coding-Standards' Command/Query naming) walks every account and returns per-account `LedgerSum`/`RawSqlLedgerSum`/`LedgerEntryCount`/`IsReconciled` plus a report-level `AllReconciled`. 3 unit tests (all-reconciled, a simulated divergence, empty report) plus 2 integration tests (real Testcontainers Postgres — real debits/credits through the actual handlers, confirming the raw-SQL path actually agrees with LINQ for real, which a mock can't exercise). Wired into `Wallet.Api`'s DI but — consistent with Debit/Credit's Day 24 reasoning — not yet exposed as a scheduled job or HTTP endpoint, since that still needs the same undecided caller-auth model. Full `Wallet.Tests` suite: 34/34 passing. Full solution builds clean. Committed to `main` (`feat(wallet): add ledger reconciliation report`), not yet pushed.

**Session note:** this session picked up after another interruption — Docker Desktop and the K8s cluster were both down at session start and needed ~10-15 minutes to come back up before Postgres/RabbitMQ port-forwards worked; no code was lost, Day 25 was already complete and just needed re-verification before Days 26-29 proceeded.

**Phase 6 note:** Day 30 (ADR-0006/0007, concurrency journal entry, Phase 6 DoD review) is the last day of this phase — a phase boundary follows it (Phase 7, Payment Service), so per CLAUDE.md's checkpoint rule, stop after Day 30 and wait for review before pushing or starting Phase 7.

**ADR note:** ADR authorship was reversed in a prior session (2026-08-17) per explicit direction — Claude now drafts full ADRs (Context/Decision/Options/Trade-offs/Consequences), user reviews/accepts. ADR-0001 through ADR-0005 are all written and Accepted in `docs/adr/` (`docs/Technology-Decisions.md`'s index reflects this). A Phase 5 Learning Journal entry is still outstanding — worth writing before too much Phase 6 context displaces it.

**Infra note (not part of Phase 5 scope, carried from the prior session):** Docker Desktop was reinstalled fresh (v29.7.2, WSL2 backend) with data at `D:\Docker_Data\DockerDesktopWSL`; NuGet's global cache lives at `D:\MyCash\.nuget\packages`. Single-node Kubernetes (7168 MiB RAM, 6 CPUs) is the proven-stable config after a 3-node attempt failed on genuine resource starvation. `docs/Deployment-Strategy.md` still needs an update reflecting the Hyper-V→WSL2 backend change and new data location — not yet done, tracked as documentation debt. An orphaned ~3.47GB file at `C:\ProgramData\DockerDesktop\vm-data\DockerDesktop.vhdx` needs manual elevated cleanup by the user, not urgent. `kube-prometheus-stack` Grafana was crash-looping from resource contention as of the last check — should be reconfirmed healthy before considering observability fully verified again, but does not block Identity/Phase 5 work.

*(Update this section at the end of every session so the next session picks up in the right place.)*

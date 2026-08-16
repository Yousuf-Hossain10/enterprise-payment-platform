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

**Day:** 22 (complete — end of Phase 5)
**Phase:** 5 — Identity Service (complete; Phase 6 — Wallet Service — not yet started)
**Last completed:** Day 22 — Login rate limiting + Identity threat-model notes, closing out Phase 5. `LoginRateLimiting.cs` adds a fixed-window rate limiter (`Microsoft.AspNetCore.RateLimiting`, no external package) on `POST /api/v1/auth/login`, partitioned by client IP: 5 attempts/minute, no queueing, 429 with an RFC 7807 `application/problem+json` body on rejection — wired via `AddRateLimiter`/`UseRateLimiter` in `Program.cs` and `[EnableRateLimiting]` on the `Login` action. Caught and fixed a real bug during test-writing: `WriteAsJsonAsync` was silently overriding the explicitly-set `application/problem+json` content type back to `application/json` — fixed by passing `contentType` directly to the call. Two new `LoginRateLimitingTests` (TestServer-based, isolated from the full host/DB) confirm 5 permitted + 6th rejected. Verified for real against the live running `Identity.Api`: 7 rapid login attempts with bad credentials returned 401×5 then 429×2, with the correct problem+json body and content-type header, confirmed via curl. Full Identity.Tests suite now 30/30 passing (28 prior + 2 new). Added an early (pre-Phase 16) threat-model note to `docs/Security-Model.md` §7.1 covering brute-force login protection and refresh-token-theft mitigation (opaque high-entropy tokens hashed at rest, rotation-on-use, explicit revocation), and flagging revoke-entire-chain-on-reuse-detected as a Phase 16 hardening item rather than a Phase 5 gap. Full solution builds clean (0 warnings/errors). Three local commits to `main` (`chore: ignore relocated NuGet package cache directory`, `feat(identity): add login rate limiting`, `docs: security notes for identity brute-force and token-theft mitigations`) — not yet pushed.

**Phase 5 exit note:** Two ADR decision points are pending and belong to the user, not written here: ADR-0004 (Password Hashing Algorithm — Argon2id parameters already implemented, decision writeup outstanding) and ADR-0005 (JWT vs Opaque Tokens + Introspection — hybrid already implemented: JWT access + opaque refresh, decision writeup outstanding). A Phase 5 Learning Journal entry is also outstanding. Per CLAUDE.md's phase-boundary checkpoint rule, this session stops here and waits for review before pushing to `origin` or starting Phase 6 (Wallet Service).

**Infra note (not part of Phase 5 scope, carried from the prior session):** Docker Desktop was reinstalled fresh (v29.7.2, WSL2 backend) with data at `D:\Docker_Data\DockerDesktopWSL`; NuGet's global cache lives at `D:\MyCash\.nuget\packages`. Single-node Kubernetes (7168 MiB RAM, 6 CPUs) is the proven-stable config after a 3-node attempt failed on genuine resource starvation. `docs/Deployment-Strategy.md` still needs an update reflecting the Hyper-V→WSL2 backend change and new data location — not yet done, tracked as documentation debt. An orphaned ~3.47GB file at `C:\ProgramData\DockerDesktop\vm-data\DockerDesktop.vhdx` needs manual elevated cleanup by the user, not urgent. `kube-prometheus-stack` Grafana was crash-looping from resource contention as of the last check — should be reconfirmed healthy before considering observability fully verified again, but does not block Identity/Phase 5 work.

*(Update this section at the end of every session so the next session picks up in the right place.)*

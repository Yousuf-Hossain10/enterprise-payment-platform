# ADR-0003: Monorepo vs. Polyrepo

**Status:** Accepted
**Date:** 2026-08-17 (backfilled — reflects the Phase 2 repository scaffold already in place since Days 5–6)
**Deciders:** Platform Architect (solo capstone)

## Context

Five services (Identity, Wallet, Payment, Notification, Audit), a Gateway, an Angular frontend, and four shared `BuildingBlocks` libraries (`Common`, `Messaging`, `Observability`, `Security`) all need somewhere to live. The question is whether that's one repository or one-per-deployable-unit — a real fork in the road for any multi-service system, solo project or not.

The repo is already structured as a monorepo (`docs/Folder-Structure.md`; `src/Services/*`, `src/BuildingBlocks/*`, `src/Gateway`, `src/Frontend`, all under one root, one `.git`), decided at Phase 1/2 per `docs/Technology-Decisions.md`'s Phase 1 Decisions section — this ADR is the formal write-up of a decision the repository layout already reflects.

Constraints specific to this project: solo developer (no cross-team coordination cost to a monorepo, which is the thing polyrepo usually optimizes for), no CI pipeline until Phase 14 (so "coarser CI trigger surface" is a future cost, not a current one), and `BuildingBlocks` libraries that every service depends on and that change during active development (e.g. `BuildingBlocks.Security`'s JWT/permission handling landed in Phase 5, consumed immediately by Identity).

## Decision

Use a single monorepo (`enterprise-payment-platform`) for all services, the Gateway, the Angular frontend, and the shared `BuildingBlocks` libraries.

## Options Considered

### Option A: Monorepo
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low for a solo developer — one clone, one `dotnet build`/`ng build`, one place to look for anything |
| Cost | N/A locally; the real cost shows up in CI (Phase 14) as a coarser trigger surface — a change to one service's test file can trigger a pipeline that touches unrelated services unless path filters are added |
| Scalability | Fine at 5 services + Gateway + frontend; monorepo tooling pain (slow CI, unclear ownership boundaries) tends to show up at team scale, which doesn't apply here |
| Learning value | High — a shared `BuildingBlocks.*` library consumed by multiple services (already true: `Identity.Infrastructure` → `BuildingBlocks.Security`) is easiest to evolve and test as part of one repo, since a breaking change and its fix land in the same commit instead of a cross-repo version-bump dance |

**Pros:** atomic cross-service commits (a `BuildingBlocks` change and the services that consume it update together, never out of sync), one place to search/grep across the whole platform, no package-registry step needed to consume shared libraries (plain `ProjectReference`s, as seen throughout `Identity.*.csproj` and `Wallet.*.csproj`), simplest possible setup for a single developer
**Cons:** every service's CI eventually runs off the same repo event stream unless explicitly scoped (a Phase 14 concern, not solved yet); no natural repo-level access-control boundary between services (irrelevant for a solo project, real for a multi-team one)

### Option B: Polyrepo (one repository per service/library)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Higher for a solo developer — 9+ repositories (5 services, Gateway, frontend, 4 `BuildingBlocks` libraries) to clone, version, and keep in sync |
| Cost | `BuildingBlocks.*` libraries would need to be published as versioned NuGet packages (even if to a local/private feed) and explicitly bumped in every consuming service — real overhead with no solo-project benefit |
| Scalability | Better at team scale (independent ownership, independent release cadence, independent CI blast radius per repo) — the scenario this pattern is actually designed for |
| Learning value | Real, but it's a different lesson (package versioning, cross-repo dependency management, release coordination) than what this capstone's phases are built to teach — those phases assume shared libraries are just there, not versioned artifacts |

**Pros:** clean ownership boundaries, independent CI/release per repo, no risk of one service's build failure blocking visibility into another's
**Cons:** for a solo developer, this is pure overhead — every `BuildingBlocks` change becomes "commit here, bump version, publish, update reference in N consuming repos" instead of one commit; nothing in this project's team structure (a team of one) benefits from the isolation polyrepo provides

## Trade-off Analysis

Polyrepo earns its cost when independent teams need independent release cadences and hard ownership boundaries enforced at the repository level — neither applies to a solo capstone. The one real monorepo cost that matters here — CI trigger granularity — doesn't exist yet (no CI until Phase 14) and is solvable later with path-based workflow filters without touching the repository layout itself. Meanwhile the main monorepo benefit — atomic commits across `BuildingBlocks` and its consumers — has already paid off in practice: Phase 5's JWT/permission work in `BuildingBlocks.Security` and its immediate consumption by `Identity.Infrastructure` landed as part of the same continuous history, with no version-bump step in between.

## Consequences

- Cross-cutting changes (a `BuildingBlocks.Common.Result<T>` signature change, for example) touch every consumer in the same commit, which is easier to review and impossible to leave half-migrated
- Phase 14's CI setup will need explicit path filters (e.g. `dotnet test` scoped to changed projects) to avoid every commit running every service's full test suite — a known, deferred cost, not an oversight
- Revisit trigger: if this project ever needed genuinely independent deploy cadences per service (e.g. simulating separate teams with separate release trains), that would be the moment to reconsider — not expected to happen within this capstone's scope

## Action Items

1. [x] Scaffold the monorepo layout per `docs/Folder-Structure.md` (Phase 2, Days 5–6)
2. [x] Wire `BuildingBlocks.*` libraries into consuming services via plain `ProjectReference`s
3. [ ] Add path-based CI trigger filters when the pipeline is built (Phase 14, Day 71+)

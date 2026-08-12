# Folder Structure

This is the monorepo layout the repository scaffolds to in Phase 2 (Repository Initialization). It's reproduced here exactly, in advance, so Phase 2 has a fixed target to scaffold against rather than inventing structure on the fly — per the tutorial's own Phase 2 Definition of Done: *"Folder structure matches `Folder-Structure.md` exactly (update the doc if you deviate)."*

If Phase 2 implementation ends up deviating from this layout for a good reason, this file gets updated to match reality at that point, and the reason gets noted inline.

```text
enterprise-payment-platform/
├── docs/
│   ├── diagrams/
│   └── *.md                      # Phase 1 documents
├── scripts/
│   ├── bootstrap.sh
│   └── bootstrap.ps1
├── src/
│   ├── BuildingBlocks/
│   │   ├── BuildingBlocks.Common/
│   │   ├── BuildingBlocks.Messaging/
│   │   ├── BuildingBlocks.Observability/
│   │   └── BuildingBlocks.Security/
│   ├── Services/
│   │   ├── Identity/
│   │   │   ├── Identity.Api/
│   │   │   ├── Identity.Application/
│   │   │   ├── Identity.Domain/
│   │   │   ├── Identity.Infrastructure/
│   │   │   └── Identity.Tests/
│   │   ├── Wallet/            # same sub-structure
│   │   ├── Payment/           # same sub-structure
│   │   ├── Notification/      # same sub-structure
│   │   └── Audit/             # same sub-structure
│   ├── Gateway/
│   │   └── Gateway.Api/        # YARP/Ocelot BFF
│   └── Frontend/
│       └── payment-platform-ui/   # Angular app
├── deploy/
│   ├── k8s/                    # raw manifests, Phase 12
│   └── helm/                   # charts, Phase 13
├── .github/
│   └── workflows/               # CI/CD, Phase 14
├── PaymentPlatform.sln
└── README.md
```

## Notes on Each Top-Level Folder

- **`docs/`** — every Phase 1 planning document (this file, `Architecture.md`, `Technology-Decisions.md`, etc.), plus `docs/diagrams/` for standalone `.mmd` files and `docs/adr/` for numbered ADRs (`ADR-Template-and-Starter-Log.md`).
- **`scripts/`** — the Phase 3 bootstrap/teardown scripts (`bootstrap.sh` / `bootstrap.ps1` for cross-platform local setup, `teardown.sh`). Anything that stands up or tears down local infrastructure lives here, not scattered per-service.
- **`src/BuildingBlocks/`** — the four Phase 4 shared libraries (`Common`, `Messaging`, `Observability`, `Security`). Every service references these instead of reimplementing cross-cutting concerns; nothing service-specific belongs here.
- **`src/Services/<ServiceName>/`** — one folder per microservice (Identity, Wallet, Payment, Notification, Audit), each following the same five-project Clean Architecture split: `Api` (controllers/composition root), `Application` (handlers/use cases), `Domain` (entities, value objects, domain logic — no framework dependencies), `Infrastructure` (EF Core, repositories, outbox dispatcher, message publisher), `Tests` (unit + integration).
- **`src/Gateway/Gateway.Api/`** — the single BFF every client request goes through; no service is called directly by the frontend.
- **`src/Frontend/payment-platform-ui/`** — the Angular application, scaffolded in Phase 2 alongside the backend so `dotnet build` and `ng build` can both be verified from day one.
- **`deploy/k8s/`** and **`deploy/helm/`** — raw Kubernetes manifests (Phase 12) and the Helm charts that later parameterize them per environment (Phase 13). Kept separate from `src/` since they're deployment artifacts, not application code.
- **`.github/workflows/`** — GitHub Actions pipelines, added in Phase 14 once there's something worth gating on (see the git-workflow rule in `CLAUDE.md`: no feature branches/PRs until this exists).
- **`PaymentPlatform.sln`** — the single solution file referencing every .NET project (`BuildingBlocks.*`, each service's five projects, `Gateway.Api`), so `dotnet build` at the repo root builds everything at once.
- **`README.md`** — repo-root entry point; written properly once there's something to run (Phase 2's Definition of Done requires `dotnet build` and `ng build` to succeed first).

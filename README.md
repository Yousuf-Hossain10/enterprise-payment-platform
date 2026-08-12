# Enterprise Payment Platform

A solo capstone project simulating a production-grade fintech platform: .NET 8 microservices, an Angular frontend, Kubernetes, and full observability/security/CI-CD, built incrementally across 19 phases per [`Enterprise_Payment_Platform_Tutorial.md`](Enterprise_Payment_Platform_Tutorial.md).

**This is a simulation.** No real payment data is processed and this repository is not PCI-DSS scoped — see [`docs/Security-Model.md`](docs/Security-Model.md) §5 for what that means concretely.

## Documentation

Start with [`docs/Architecture.md`](docs/Architecture.md) for the system overview and diagrams. The full `/docs` set (architecture, coding standards, service responsibilities, API guidelines, deployment/security/logging/observability strategy, and the phase-by-phase roadmap) is written before any corresponding code, per the project's Phase 1 rule.

Project process (how work is scoped and reviewed session to session) lives in [`CLAUDE.md`](CLAUDE.md).

## Repository Layout

```text
enterprise-payment-platform/
├── docs/                          # architecture & planning docs (Phase 1)
├── scripts/                       # local infra bootstrap/teardown (Phase 3)
├── src/
│   ├── BuildingBlocks/            # shared .NET libraries (Phase 4)
│   ├── Services/                  # Identity, Wallet, Payment, Notification, Audit
│   │   └── <Service>/             # Api / Application / Domain / Infrastructure / Tests
│   ├── Gateway/Gateway.Api/       # YARP-based BFF
│   └── Frontend/payment-platform-ui/  # Angular app
├── deploy/                        # k8s manifests (Phase 12) and Helm charts (Phase 13)
├── .github/workflows/             # CI/CD (Phase 14)
├── PaymentPlatform.sln
└── README.md
```

See [`docs/Folder-Structure.md`](docs/Folder-Structure.md) for the full rationale behind this layout.

## Services

| Service | Responsibility |
|---|---|
| **Identity** | Authentication, JWT issuance/rotation |
| **Wallet** | Ledger-based account balances — the only writer of money movement |
| **Payment** | Orchestrates the payment saga against Wallet |
| **Notification** | Delivers payment/wallet events as (mocked) notifications, exactly once |
| **Audit** | Independent, append-only, tamper-evident record of every domain event |

Full ownership and event contracts: [`docs/Microservice-Responsibilities.md`](docs/Microservice-Responsibilities.md).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (pinned via [`global.json`](global.json) — this repo targets 8.0.x even if a later SDK is also installed)
- [Node.js](https://nodejs.org/) + npm (for the Angular frontend)
- Docker, Kind, kubectl, Helm (from Phase 3 onward — local Kubernetes infrastructure)

## Building

**Backend** (all 30 projects — 4 shared libraries, 5 services × 5 projects each, and the Gateway):

```bash
dotnet build PaymentPlatform.sln
```

**Frontend:**

```bash
cd src/Frontend/payment-platform-ui
npm install
npx ng build
```

Both currently build clean with no application code yet — this repository is at the end of Phase 2 (Repository Initialization): the monorepo skeleton exists and builds, nothing is implemented. See [`docs/Development-Roadmap.md`](docs/Development-Roadmap.md) for what's next.

## Status

Phase 1 (Architecture & Planning) and Phase 2 (Repository Initialization) are complete. Local infrastructure (Phase 3) is next.

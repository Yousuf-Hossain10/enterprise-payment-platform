# Architecture

## Overview

The Enterprise Payment Platform is a cloud-native payment system built as a set of five independently deployable .NET 8 microservices, fronted by a single Angular client and a Gateway/BFF. Services own their data exclusively (database-per-service), communicate synchronously over HTTP only through the Gateway or direct service-to-service calls where a real-time answer is required (e.g. Payment calling Wallet), and communicate asynchronously via RabbitMQ for everything else (cross-service notifications, audit trail, eventual-consistency updates).

This document describes the system as currently designed. It will be kept up to date as implementation decisions are made — see [Technology-Decisions.md](Technology-Decisions.md) for the reasoning behind specific choices.

## Services and Responsibilities

| Service | Responsibility | Owns |
|---|---|---|
| **Identity Service** | Authenticates users, issues and rotates JWT access/refresh tokens, manages accounts | `User`, `RefreshToken` |
| **Wallet Service** | Source of truth for account balances; the only service allowed to write a ledger entry | `Account`, `LedgerEntry` |
| **Payment Service** | Orchestrates the payment lifecycle (a saga), calling Wallet to move money and publishing lifecycle events | `Payment`, payment state machine |
| **Notification Service** | Consumes domain events and delivers user-facing notifications (email/SMS, mocked) exactly once | notification delivery records |
| **Audit Service** | Consumes domain events from every other service into an independent, append-only, tamper-evident log | `AuditEntry` |

Each service is a separate Clean Architecture solution (`Api` / `Application` / `Domain` / `Infrastructure` / `Tests`) with its own Postgres database. No service reaches into another service's database — all cross-service reads go through that service's API, and all cross-service data propagation goes through published events.

## Communication Patterns

### Synchronous (HTTP)

- The **Angular frontend never talks to a service directly** — every request goes through the **Gateway** (YARP-based BFF), which is responsible for routing, auth-token validation, and rate limiting on sensitive endpoints.
- The Gateway forwards requests to the appropriate service over the cluster-internal network.
- **Payment → Wallet** is the one significant service-to-service synchronous call in the system: Payment needs a real-time, consistent answer ("did the debit succeed?") before it can decide the next state transition in its saga. This call is wrapped in Polly retry and circuit-breaker policies since Wallet's availability directly gates Payment's ability to make progress.
- All synchronous financial endpoints (`POST /payments`, wallet debit/credit) require an `Idempotency-Key` header so retries — from Polly, from the client, or from a network blip — never double-process money movement.

### Asynchronous (RabbitMQ)

- Services publish domain events through the **transactional outbox pattern**: the event is written to an `OutboxMessages` table in the same database transaction as the business change, then a background dispatcher publishes it to RabbitMQ. This guarantees a state change and its event are never inconsistent with each other, even across a crash.
- Consumers use an **idempotent-consumer pattern** (`ProcessedEvents` table keyed by event ID) so at-least-once delivery from RabbitMQ never results in a duplicate side effect (e.g. two notifications for one payment).
- Key events: `PaymentCaptured`, `PaymentFailed`, `WalletDebited`, `WalletCredited`. Notification and Audit both subscribe to the events relevant to them; Audit additionally subscribes to events from every service, since its job is a complete cross-service record.

## System Context

```mermaid
flowchart TB
    User((Customer)) --> FE[Angular Frontend]
    FE --> GW[API Gateway / BFF]
    GW --> IdSvc[Identity Service]
    GW --> WSvc[Wallet Service]
    GW --> PSvc[Payment Service]
    PSvc --> WSvc
    PSvc -.events.-> MQ[(RabbitMQ)]
    WSvc -.events.-> MQ
    MQ -.-> NSvc[Notification Service]
    MQ -.-> ASvc[Audit Service]
    IdSvc --> DB1[(Postgres - Identity)]
    WSvc --> DB2[(Postgres - Wallet)]
    PSvc --> DB3[(Postgres - Payment)]
    NSvc --> DB4[(Postgres - Notification)]
    ASvc --> DB5[(Postgres - Audit)]
```

## Container Diagram

One box per deployable, showing network boundaries. Redis is used for Gateway-level rate-limit counters (`Security-Model.md` §6); nothing else in the platform currently depends on it.

```mermaid
flowchart TB
    subgraph Client
        FE[Angular Frontend]
    end
    subgraph Edge
        GW["Gateway / BFF (YARP)"]
    end
    subgraph Services
        IdSvc[Identity Service]
        WSvc[Wallet Service]
        PSvc[Payment Service]
        NSvc[Notification Service]
        ASvc[Audit Service]
    end
    subgraph Data
        DB1[(Postgres - Identity)]
        DB2[(Postgres - Wallet)]
        DB3[(Postgres - Payment)]
        DB4[(Postgres - Notification)]
        DB5[(Postgres - Audit)]
        Redis[(Redis)]
    end
    subgraph Messaging
        MQ[(RabbitMQ)]
    end

    FE -->|HTTPS| GW
    GW -->|HTTP + JWT| IdSvc
    GW -->|HTTP + JWT| WSvc
    GW -->|HTTP + JWT| PSvc
    GW -->|HTTP + JWT| NSvc
    GW -->|HTTP + JWT| ASvc
    GW -.->|rate-limit counters| Redis
    PSvc -->|HTTP + Idempotency-Key| WSvc

    IdSvc --> DB1
    WSvc --> DB2
    PSvc --> DB3
    NSvc --> DB4
    ASvc --> DB5

    PSvc -.publish.-> MQ
    WSvc -.publish.-> MQ
    MQ -.consume.-> NSvc
    MQ -.consume.-> ASvc
```

## Component Diagram (per microservice)

Every service follows the same Clean Architecture layering (`Coding-Standards.md`), so one diagram applies structurally to all five — Identity, Wallet, Payment, Notification, and Audit. Service-specific variation: Wallet's Infrastructure layer adds the `RowVersion` optimistic-concurrency check on writes; Payment's Infrastructure layer adds the resilient Wallet HTTP client (Polly); Notification and Audit have no outbound HTTP client at all, since they're pure event consumers with no synchronous callers of their own.

```mermaid
flowchart LR
    subgraph API["*.Api"]
        Controller[Controller / Minimal API Endpoint]
        Middleware["Global Exception + Correlation-ID Middleware"]
    end
    subgraph APP["*.Application"]
        Handler[Command/Query Handler]
        Validator[FluentValidation Validator]
    end
    subgraph DOM["*.Domain"]
        Entity["Entities / Value Objects / Domain Events"]
    end
    subgraph INFRA["*.Infrastructure"]
        Repo["Repository (EF Core)"]
        Outbox[Outbox Dispatcher]
        Publisher[Message Publisher]
        Client["External HTTP Client (where applicable)"]
    end

    Controller --> Middleware --> Handler
    Handler --> Validator
    Handler --> Entity
    Handler --> Repo
    Repo --> Entity
    Handler --> Outbox
    Outbox --> Publisher
    Handler -.optional.-> Client
```

## Deployment Diagram

Kubernetes namespace layout (Phase 3), the pods within each, and the NetworkPolicy-restricted paths between them (Phase 12, Day 61 — default-deny with explicit allows).

```mermaid
flowchart TB
    Internet((Browser)) --> Ingress

    subgraph Cluster["Kind Cluster"]
        subgraph NSIngress["namespace: ingress-nginx"]
            Ingress[NGINX Ingress Controller]
        end
        subgraph NSPlatform["namespace: platform"]
            GWPod[Gateway Pods]
            IdPod[Identity Pods]
            WPod[Wallet Pods]
            PPod[Payment Pods]
            NPod[Notification Pods]
            APod[Audit Pods]
            PG[(Postgres StatefulSets)]
            MQ[(RabbitMQ StatefulSet)]
            RedisPod[(Redis)]
        end
        subgraph NSMonitoring["namespace: monitoring"]
            Prom[Prometheus]
            Graf[Grafana]
            Loki[Loki]
            Tempo[Tempo]
        end
    end

    Ingress --> GWPod
    GWPod -->|allowed| IdPod
    GWPod -->|allowed| WPod
    GWPod -->|allowed| PPod
    GWPod -->|allowed| NPod
    GWPod -->|allowed| APod
    PPod -->|allowed| WPod
    GWPod -.->|allowed| RedisPod

    IdPod --> PG
    WPod --> PG
    PPod --> PG
    NPod --> PG
    APod --> PG

    PPod -.publish.-> MQ
    WPod -.publish.-> MQ
    MQ -.consume.-> NPod
    MQ -.consume.-> APod

    GWPod -.metrics/logs/traces.-> Prom
    GWPod -.-> Loki
    GWPod -.-> Tempo
```

All service-to-service edges not shown (e.g. Identity → Wallet, Notification → Payment) are denied by default — the NetworkPolicy allow-list only covers the paths drawn above, matching the actual call graph in `Microservice-Responsibilities.md`.

## Sequence Diagrams

### Login

```mermaid
sequenceDiagram
    participant FE as Angular
    participant GW as Gateway
    participant I as Identity Service

    FE->>GW: POST /api/v1/auth/login (email, password)
    GW->>I: Forward request
    I->>I: Verify password hash (Argon2id)
    I->>I: Issue access token + refresh token
    I-->>GW: 200 OK (access token, refresh token)
    GW-->>FE: Login result (tokens stored client-side)
```

### Payment Flow

```mermaid
sequenceDiagram
    participant FE as Angular
    participant GW as Gateway
    participant P as Payment Service
    participant W as Wallet Service
    participant MQ as RabbitMQ
    participant N as Notification Service

    FE->>GW: POST /payments (Idempotency-Key)
    GW->>P: Forward request
    P->>P: Create Payment (status=Created)
    P->>W: Debit wallet (idempotency key)
    W->>W: Write ledger entry (optimistic concurrency)
    W-->>P: 200 OK / Conflict
    P->>P: Update status=Captured
    P->>MQ: Publish PaymentCaptured (outbox)
    MQ->>N: Consume event
    N->>N: Send notification (dedupe by event id)
    P-->>GW: 200 OK
    GW-->>FE: Payment result
```

### Wallet Debit (standalone)

```mermaid
sequenceDiagram
    participant Caller as Caller (Payment or Gateway)
    participant GW as Gateway
    participant W as Wallet Service

    Caller->>GW: POST /api/v1/wallet/accounts/{id}/debit (Idempotency-Key)
    GW->>W: Forward request
    W->>W: Check idempotency key against existing ledger entries
    alt Key already processed
        W-->>GW: 200 OK (original result, no-op)
    else New request
        W->>W: Write ledger entry (RowVersion optimistic concurrency check)
        alt Concurrency conflict
            W-->>GW: 409 Conflict (caller retries)
        else Success
            W->>W: Recompute balance from ledger
            W->>W: Write WalletDebited to Outbox (same transaction)
            W-->>GW: 200 OK (new balance)
        end
    end
    GW-->>Caller: Debit result
```

### Refund

```mermaid
sequenceDiagram
    participant FE as Angular
    participant GW as Gateway
    participant P as Payment Service
    participant W as Wallet Service
    participant MQ as RabbitMQ
    participant N as Notification Service

    FE->>GW: POST /api/v1/payments/{id}/refund (Idempotency-Key)
    GW->>P: Forward request
    P->>P: Verify payment is in a refundable state (Captured)
    P->>W: Credit original payer's account (idempotency key)
    W->>W: Write ledger entry (credit), recompute balance
    W-->>P: 200 OK / Conflict
    P->>P: Update status=Refunded
    P->>MQ: Publish PaymentRefunded (outbox)
    MQ->>N: Consume event
    N->>N: Send refund notification (dedupe by event id)
    P-->>GW: 200 OK
    GW-->>FE: Refund result
```

### JWT Refresh

```mermaid
sequenceDiagram
    participant FE as Angular
    participant GW as Gateway
    participant I as Identity Service

    FE->>GW: POST /api/v1/auth/refresh (refresh token)
    GW->>I: Forward request
    I->>I: Look up refresh token by hash
    alt Token invalid, expired, or already revoked
        I-->>GW: 401 Unauthorized
    else Token valid
        I->>I: Revoke used token, issue new access+refresh pair (rotation)
        I->>I: Mark old token's ReplacedByTokenHash
        I-->>GW: 200 OK (new token pair)
    end
    GW-->>FE: Refresh result (silent refresh, no user interaction)
```

### Notification

```mermaid
sequenceDiagram
    participant MQ as RabbitMQ
    participant N as Notification Service
    participant Ext as Email/SMS Provider (mocked)

    MQ->>N: Deliver PaymentCaptured (at-least-once)
    N->>N: Check ProcessedEvents table for event id
    alt Already processed
        N->>N: Ack, no-op
    else New event
        N->>N: Render notification template
        N->>Ext: Send notification (mocked)
        N->>N: Record ProcessedEvents entry + delivery record
        N->>N: Ack message
    end
```

### Audit Logging

```mermaid
sequenceDiagram
    participant MQ as RabbitMQ
    participant A as Audit Service

    MQ->>A: Deliver domain event (any service)
    A->>A: Check ProcessedEvents table for event id
    alt Already processed
        A->>A: Ack, no-op
    else New event
        A->>A: Compute hash of entry (previous hash + payload)
        A->>A: Append AuditEntry (immutable, hash-chained)
        A->>A: Record ProcessedEvents entry
        A->>A: Ack message
    end
```

## Design Principles Driving the Architecture

- **Database-per-service** — no shared schema, ever. Cross-service consistency is handled via events, not joins.
- **Money movement is append-only and idempotent** — Wallet never overwrites a balance in place without an idempotency check and optimistic concurrency guard (`RowVersion`); every balance is derivable from its ledger.
- **A saga, not a distributed transaction** — Payment orchestrates multi-step financial workflows explicitly, with defined compensating/failure paths, rather than relying on two-phase commit across services.
- **Independent audit trail** — Audit does not read from other services' databases; it only trusts what came through the event bus, so a bug in another service's direct-write path can't silently corrupt the audit record.
- **Everything crosses the wire with a correlation ID** so a request can be traced end-to-end across Gateway → services → async consumers (see `Logging-Strategy.md` / `Observability-Strategy.md`, written Day 3).

## Status

This document reflects the target architecture agreed at the start of the project (Day 1, Phase 1). No code exists yet. Per Phase 17, this file will be refreshed to match the final as-built system before the project is considered complete.

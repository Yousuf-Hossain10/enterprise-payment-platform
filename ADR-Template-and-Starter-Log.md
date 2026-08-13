# Architecture Decision Records — Template & Decision Log

*Companion to the Enterprise Payment Platform tutorial. Replaces the single `Technology-Decisions.md` file suggested in Phase 1 with the more standard practice: one numbered file per decision, stored in `docs/adr/`.*

Writing your own ADRs — not reading pre-written ones — is where the real learning happens on this project. This file gives you the format, two fully worked examples so you can see what "good" looks like, and a log of every decision point you'll hit across the 17 phases. Write the rest yourself, in order, as you reach each phase. Don't do them all up front — the reasoning is sharper when you're mid-implementation and can feel the trade-offs.

**File naming convention:** `docs/adr/ADR-0001-short-title.md`, numbered sequentially, never renumbered or deleted even if superseded (mark superseded ADRs as `Status: Superseded by ADR-00xx`).

---

## Template

```markdown
# ADR-[number]: [Title]

**Status:** Proposed | Accepted | Deprecated | Superseded
**Date:** [Date]
**Deciders:** [You — but name the "role" you're deciding as, e.g. "Platform Architect"]

## Context
[What is the situation? What forces are at play? What constraints matter —
time, cost, team size (just you), existing tech already committed to?]

## Decision
[What is the change we're proposing? One or two sentences, unambiguous.]

## Options Considered

### Option A: [Name]
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low / Med / High |
| Cost | Assessment |
| Scalability | Assessment |
| Learning value | Assessment — this row is unique to your capstone: sometimes the "harder" option is correct *because* you're optimizing for depth of understanding, not shipping speed |

**Pros:** [List]
**Cons:** [List]

### Option B: [Name]
[Same format]

## Trade-off Analysis
[Key trade-offs between options with clear reasoning — this is the section
that actually proves you understand the problem, not just the menu of tools]

## Consequences
- [What becomes easier]
- [What becomes harder]
- [What you'll need to revisit later, and under what condition]

## Action Items
1. [ ] [Implementation step]
2. [ ] [Follow-up]
```

---

## Worked Example 1 — Infrastructure Decision

# ADR-0001: Message Broker — RabbitMQ vs. Kafka

**Status:** Accepted
**Date:** 2026-07-28
**Deciders:** Platform Architect (solo capstone)

## Context
The platform needs async communication between Payment, Wallet, Notification, and Audit services — primarily for domain events like `WalletDebited` and `PaymentCaptured`, published via the outbox pattern. Message volume is low (this is a simulation, not a production fintech company), but the patterns (idempotent consumers, DLQs, at-least-once delivery) need to be realistic.

## Decision
Use RabbitMQ as the message broker for all inter-service async communication.

## Options Considered

### Option A: RabbitMQ
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low — simple exchange/queue model, quick to reason about |
| Cost | Free, lightweight enough to run in Kind alongside everything else |
| Scalability | Sufficient for this project's volume; not built for Kafka-scale throughput |
| Learning value | High — teaches queue-based messaging, DLQs, and consumer idempotency without Kafka's operational complexity (partitions, consumer groups, offsets) getting in the way of the core lesson |

**Pros:** simple mental model, easy local setup, well-documented .NET client, teaches the fundamentals (ack/nack, DLQ, routing) clearly
**Cons:** not built for high-throughput event streaming or replay-from-any-point use cases; a real fintech at scale would likely reconsider

### Option B: Kafka
| Dimension | Assessment |
|-----------|------------|
| Complexity | High — partitions, consumer groups, offset management |
| Cost | Higher resource footprint locally (Zookeeper/KRaft, brokers) |
| Scalability | Excellent — built for this |
| Learning value | High in a different direction — event streaming, log-based architecture, replay — but adds operational overhead that would slow down Phases 4–9 without changing the core lessons this project is built to teach |

**Pros:** industry-standard for event streaming at scale, teaches log-based architecture, strong replay semantics
**Cons:** meaningfully heavier to run and operate locally; the extra complexity doesn't serve this project's core goal, which is correctness patterns (outbox, idempotency, sagas), not streaming-scale throughput

## Trade-off Analysis
The deciding factor isn't "which is more impressive on a resume" — it's which choice keeps the focus on the patterns this capstone is actually testing (outbox, idempotent consumers, sagas, DLQs). RabbitMQ delivers all of those with less incidental complexity. Kafka is a legitimate follow-up exploration once the core platform is stable — e.g., as a Phase 18 stretch goal: "swap RabbitMQ for Kafka and observe what changes."

## Consequences
- Easier local development, faster Phase 3 bootstrap
- If message volume/throughput requirements ever became real, this decision would need revisiting
- Revisit trigger: if a future exploration wants to demonstrate event replay or stream processing, that's the moment to evaluate Kafka as a follow-up ADR

## Action Items
1. [x] Add RabbitMQ Helm install to `bootstrap.sh`
2. [x] Document DLQ and retry conventions in `Logging-Strategy.md`

---

## Worked Example 2 — Algorithmic / Pattern Decision

# ADR-0002: Payment Saga — Orchestration vs. Choreography

**Status:** Accepted
**Date:** 2026-07-28
**Deciders:** Platform Architect (solo capstone)

## Context
Capturing a payment requires coordinating a debit on the Wallet service and a downstream notification, with correctness guaranteed even if a step fails partway through. This needs a saga pattern — the question is orchestration (one service directs the sequence) or choreography (services react to each other's events with no central coordinator).

## Decision
Use an orchestrated saga, with the Payment service as the orchestrator.

## Options Considered

### Option A: Orchestration (Payment service coordinates)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — one service owns the state machine and failure handling |
| Cost | N/A |
| Scalability | Fine at this scale; the orchestrator can become a bottleneck at very high scale, not a concern here |
| Learning value | High — makes the failure-handling logic explicit and testable in one place, which matters for the fault-injection test this project requires |

**Pros:** the full payment lifecycle is visible in one place, easy to test failure paths, matches the sequence diagrams from Phase 1 directly
**Cons:** Payment service becomes a single point of coordination logic; if it's down, no new sagas start (acceptable trade-off here)

### Option B: Choreography (services react to each other's events)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Higher, in a subtler way — the "flow" is implicit, spread across every service's event handlers |
| Cost | N/A |
| Scalability | Better decoupling at very large service counts |
| Learning value | Real, but the failure-handling logic becomes harder to trace and test end-to-end — a poor fit for a *first* saga implementation |

**Pros:** no central coordinator, services are more independently deployable
**Cons:** debugging "what happened to this payment" means tracing events across five services' logs instead of reading one state machine; much easier to accidentally create a distributed deadlock or an unhandled edge case that nobody's event handler catches

## Trade-off Analysis
For a five-service platform where you're still building intuition for distributed failure modes, orchestration keeps the "what can go wrong and how do we recover" logic legible in one file. Choreography's benefits (decoupling, independent scaling) matter more at a scale this project isn't operating at. This is explicitly a case where the simpler-to-reason-about choice is also the better teaching tool.

## Consequences
- The Payment service has more responsibility and more tests than a choreography design would put on any single service
- The fault-injection test (kill Wallet mid-saga) has one clear place to assert against: Payment's state machine
- Revisit trigger: if you later add a sixth or seventh service to the saga and the orchestrator's logic starts feeling unwieldy, that's a legitimate moment to explore choreography or a hybrid approach as a follow-up ADR

## Action Items
1. [x] Implement `Payment` state machine with explicit legal-transition guards
2. [x] Write the fault-injection test as part of Phase 7's Definition of Done

---

## Decision Log — Write These Yourself

Work through these in phase order. For each, spend real time on the "why," not just the "what" — the goal is being able to defend the decision in a design review, not just make one.

| ADR # | Phase | Decision Point | Starter Question |
|---|---|---|---|
| ADR-0003 | 2 | Monorepo vs. polyrepo | What does a monorepo cost you in CI complexity, and what does it save you in cross-service refactoring? |
| ADR-0004 | 5 | Password hashing algorithm (Argon2id vs. PBKDF2 vs. bcrypt) | Why is SHA-256 alone wrong here, and what specifically does a memory-hard algorithm defend against? |
| ADR-0005 | 5 | JWT vs. opaque tokens + introspection | What do you give up in revocation control by choosing self-contained JWTs? |
| ADR-0006 | 6 | Immutable ledger vs. mutable balance column | Walk through what happens to a mutable balance column under two concurrent debits without your ledger design — write out the race condition explicitly. |
| ADR-0007 | 6 | Optimistic vs. pessimistic concurrency control | At what request volume would optimistic concurrency's retry rate become a real problem? |
| ADR-0008 | 9 | Append-only audit table vs. dedicated event store (e.g. EventStoreDB) | What does a purpose-built event store give you that a Postgres table with a hash chain doesn't? |
| ADR-0009 | 10 | State management: NgRx vs. Angular Signals | At what point does application state become complex enough to justify NgRx's boilerplate? |
| ADR-0010 | 10 | Gateway: YARP vs. Ocelot | What's actually different between them beyond "one is Microsoft's"? |
| ADR-0011 | 12 | Namespace-per-environment vs. cluster-per-environment | What blast-radius difference does this create if a NetworkPolicy is misconfigured? |
| ADR-0012 | 12 | Secrets: plain K8s Secrets vs. external-secrets/Vault, and when to switch | Is base64-encoded-in-etcd an acceptable risk at each project stage, and why or why not? |
| ADR-0013 | 13 | Helm vs. Kustomize | What's the philosophical difference between "templating" and "patching," and which one fits a monorepo with many similar services better? |
| ADR-0014 | 14 | Self-hosted vs. GitHub-hosted CI runners | What are you actually gaining from self-hosting for a solo project, versus what it's costing you in maintenance? |
| ADR-0015 | 15 | Tracing backend: Tempo vs. Jaeger | Beyond "Grafana-native," what operational differences matter at your scale? |
| ADR-0016 | 16 | Service mesh / mTLS — adopt now or defer | What does NetworkPolicy alone *not* protect against that mTLS would? |
| ADR-0017 | 17 | Load testing tool: k6 vs. Gatling vs. JMeter | What does each optimize for (scripting ergonomics vs. protocol coverage vs. ecosystem maturity)? |
| ADR-0018 | 18 | Risk service fail-open vs. fail-closed on outage | If you were the business owner, not the engineer, which failure mode would you actually want — and does that change your answer? |
| ADR-0019 | 19 | Reporting service: rebuild-on-demand vs. always-on replica | What does "always eventually consistent" cost you operationally that a rebuild-on-demand model avoids, and vice versa? |
| ADR-0020 | 3 | Local cluster: `kind` vs. Docker Desktop Kubernetes | This one wasn't chosen deliberately — it was forced by `kind` being unreachable from the dev sandbox. Now that you're not blocked, is Docker Desktop Kubernetes still the right call for the rest of the project, or is `kind`'s multi-node support worth going back for before Phase 12's NetworkPolicy/HPA work needs it? |

Add new rows as you discover more decisions mid-implementation — the log above is a floor, not a ceiling. ADR-0020 is a live example: Day 7 hit a real infrastructure substitution that wasn't on the original list, and it got added rather than left undocumented.

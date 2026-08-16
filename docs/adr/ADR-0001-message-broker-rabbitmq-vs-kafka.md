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

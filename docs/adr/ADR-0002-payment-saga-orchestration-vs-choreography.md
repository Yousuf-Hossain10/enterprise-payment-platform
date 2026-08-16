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

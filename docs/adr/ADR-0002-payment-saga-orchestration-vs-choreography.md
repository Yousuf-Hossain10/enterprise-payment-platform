# ADR-0002: Payment Saga — Orchestration vs. Choreography

**Status:** Accepted
**Date:** 2026-07-28 (decision); finalized with real implementation notes 2026-08-18 (Day 38, end of Phase 7)
**Deciders:** Platform Architect (solo capstone)

## Context

Capturing a payment requires coordinating a debit on the Wallet service and a downstream notification, with correctness guaranteed even if a step fails partway through. This needs a saga pattern — the question is orchestration (one service directs the sequence) or choreography (services react to each other's events with no central coordinator).

This ADR was originally written at Phase 1/2, before any Payment code existed, as a worked example (`ADR-Template-and-Starter-Log.md`). Phase 7 (Days 31-37) has since built the actual saga: `Payment.Domain/Payment.cs` (the state machine), `Payment.Application/CapturePayment.cs` (the orchestrator), `Payment.Infrastructure/WalletClient.cs` (the resilient call to Wallet), and `Payment.Tests/FaultInjectionTests.cs` (the fault-injection test this ADR's Consequences section anticipated). The sections below are updated to reference what was actually built, not just what was predicted.

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

**Confirmed, not just predicted:** `CapturePaymentCommandHandler.HandleAsync` (`Payment.Application/CapturePayment.cs`) is genuinely the *entire* saga in one method — load payment, authorize, call Wallet, branch on the result, persist the terminal state. Every failure mode the fault-injection test needed to exercise (Wallet returns 500 persistently, Wallet is unreachable, Wallet fails transiently then recovers) is driven through that one method with no cross-service log correlation required to understand what happened. The orchestration choice is what made Day 36's fault-injection test straightforward to write at all — there was exactly one place to assert against, exactly as predicted.

**A design detail the original ADR didn't anticipate, but that orchestration made natural:** the saga deliberately does *not* persist the in-memory `MarkAuthorized()` transition on its own — only the terminal outcome (`Failed`/`Captured`) is ever saved. If the process crashes between authorization and the Wallet call returning, the stored payment still shows `Created`, so a retried capture starts clean rather than getting stuck in a half-persisted `Authorized` limbo. This crash-safety property is a direct consequence of having one orchestrator with full control over exactly when state gets written - a choreography design, with each service independently persisting its own local view of the saga's progress, would have made this specific guarantee much harder to reason about (which service's write, if any, represents "the" authoritative Authorized state?).

## Consequences

- The Payment service has more responsibility and more tests than a choreography design would put on any single service - confirmed: `Payment.Tests` sits at 71 tests as of Day 37, more than any other single service's test count at the equivalent point in its phase, precisely because the saga's full behavior space (happy path, Wallet failure, Wallet timeout/unreachable, transient-then-recovers, illegal state transitions) all lives in one place to test
- The fault-injection test (kill Wallet mid-saga) has one clear place to assert against: Payment's state machine - confirmed in `FaultInjectionTests.cs`, which runs a real disposable Kestrel server standing in for Wallet and asserts against `CapturePaymentCommandHandler`'s single code path for all three fault scenarios
- Revisit trigger: if you later add a sixth or seventh service to the saga and the orchestrator's logic starts feeling unwieldy, that's a legitimate moment to explore choreography or a hybrid approach as a follow-up ADR - not triggered: Phase 7 only ever coordinates one downstream call (Wallet), so the orchestrator never grew past a single, easily-readable method

## Action Items

1. [x] Implement `Payment` state machine with explicit legal-transition guards (`Payment.Domain/Payment.cs`, Day 31)
2. [x] Implement the orchestrator itself (`CapturePaymentCommandHandler`, `Payment.Application/CapturePayment.cs`, Day 33)
3. [x] Write the fault-injection test as part of Phase 7's Definition of Done (`Payment.Tests/FaultInjectionTests.cs`, Day 36)
4. [x] Contract tests documenting the Wallet/Notification boundaries this orchestrator depends on (`WalletContractTests.cs`, `NotificationContractTests.cs`, Day 37)

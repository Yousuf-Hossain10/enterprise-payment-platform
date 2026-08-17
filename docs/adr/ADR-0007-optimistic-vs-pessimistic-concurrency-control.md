# ADR-0007: Optimistic vs. Pessimistic Concurrency Control

**Status:** Accepted
**Date:** 2026-08-17
**Deciders:** Platform Architect (solo capstone)

## Context

ADR-0006 establishes that balance is always computed from `LedgerEntry` rows, never stored directly — which pushes the concurrency question onto a different surface: what stops two concurrent debits/credits against the *same account* from both reading a balance, both deciding they have enough funds, and both writing, even though the ledger design itself makes the resulting numbers safe? Something has to serialize concurrent writers against the same `Account` row, or reject the loser cleanly.

What's implemented (`Wallet.Domain/Account.cs`, `Wallet.Infrastructure/Configurations/AccountConfiguration.cs`): `Account.RowVersion` is an optimistic concurrency token backed by Postgres' native `xmin` system column (`.IsRowVersion().HasColumnName("xmin").HasColumnType("xid")` — no app-maintained counter, no explicit locking). Every debit/credit reads the account, stages its change (`Account.LastModifiedAtUtc` bump + new `LedgerEntry`), and only finds out at `SaveChangesAsync` time whether it won or lost the race — a lost race throws `DbUpdateConcurrencyException`, translated to `ConcurrencyConflictException` (`AccountRepository.SaveChangesAsync`), which `LedgerWriter.ApplyAsync` catches and retries with jittered backoff (Day 28) rather than surfacing to the caller on the first loss.

This is optimistic concurrency control, chosen implicitly by Day 23's `IsRowVersion()` mapping and made real by Days 24-28's write path. This ADR is the formal write-up per `ADR-Template-and-Starter-Log.md`'s framing for ADR-0007: *at what request volume would optimistic concurrency's retry rate become a real problem?*

**Measured data, not a guess (Day 28):** 20 truly concurrent debits against one account, capacity for 10 to succeed.
- No retry: 0 of the 20 losers ever got a second chance — success capped by how many happened to win the very first race (measured: 6/10, deterministically, every run, since ties resolve the same way under identical timing).
- Retry with no jitter (immediate retry, `MaxAttempts=5`): still capped at 6/10 — every losing attempt reloads and retries at essentially the same instant as every other loser, so they collide again in lockstep each retry round; one winner per round regardless of how many contenders remain.
- Retry with jittered backoff (1-20ms random delay, `MaxAttempts=25`): 10/10 (the theoretical maximum) across 18 consecutive runs.

This data answers the starter question directly: the real-world limiting factor for optimistic concurrency on a hot row isn't "the account gets too busy," it's "the retry strategy doesn't de-synchronize contenders." A bounded-attempts, jittered-backoff retry loop handled 20-way contention on a single account with a 100% success rate up to the account's actual capacity. The volume at which this *would* become a real problem is one where either (a) contention is sustained rather than a single burst (a hot account processing hundreds of writes/second continuously, not 20 at once), or (b) `MaxAttempts` (currently 25) is exhausted before jitter has room to spread contenders out — neither is close to this project's actual load profile, but both are concrete, measurable thresholds rather than a vague "eventually."

## Decision

Use optimistic concurrency control (Postgres `xmin` as the `RowVersion` token) for all `Account` writes, with a bounded, jittered-backoff retry loop (`LedgerWriter.ApplyAsync`) rather than pessimistic locking.

## Options Considered

### Option A: Pessimistic locking (`SELECT ... FOR UPDATE`)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — requires an explicit transaction held open across the read-check-write sequence, with careful attention to lock duration and deadlock avoidance across code paths |
| Cost | A losing writer *blocks* (waits on the lock) rather than fails fast - no wasted retry work, but throughput is bounded by how long each transaction holds the lock |
| Scalability | Serializes all writers to a hot account into a queue; predictable but a hard ceiling under sustained contention (lock wait time compounds) |
| Learning value | Real, but teaches a different lesson (explicit locking, deadlock reasoning) than the one this phase is built around (safe retry under contention, idempotency, reconciliation) |

**Pros:** no wasted work on doomed writes (a blocked writer isn't burning CPU retrying), no risk of retry-storm behavior like the lockstep collision this project actually measured and had to fix
**Cons:** every write against a hot account queues behind the currently-open transaction, including reads that only need the current balance for a decision; a long-held lock (e.g. a slow downstream call inside the transaction) stalls every other writer, not just the ones that would have conflicted; deadlock risk if a future code path ever needs to lock two accounts in one transaction (e.g. a future transfer feature) without a strict lock-ordering discipline

### Option B: Optimistic concurrency control, `xmin` + bounded jittered retry (implemented)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — no explicit lock management, but the retry loop itself needed real tuning (Day 28) to actually work under contention, which wasn't obvious up front |
| Cost | No blocking - writers proceed optimistically and only pay a cost (a wasted staged write + a retry) when they actually lose a race, which is cheap relative to holding a lock open |
| Scalability | Measured to hold up at 20-way contention on a single account with correct jitter/attempt tuning (Day 28); degrades gracefully (more retries, not a hard queue) as contention increases, up to the point `MaxAttempts` is exhausted |
| Learning value | High — this is the pattern most real high-throughput systems reach for over pessimistic locking for exactly this reason, and building it revealed a genuinely non-obvious failure mode (lockstep retry collision) that a naive "just add retries" instinct wouldn't have caught without measuring |

**Pros:** no blocking, no lock-hold-duration risk, no deadlock surface to reason about (no explicit locks exist at all), degrades gracefully under load rather than hitting a hard wall, and the retry loop is entirely application-level code that's directly testable (`ConcurrentDebitTests`) rather than relying on database lock-timeout behavior
**Cons:** naive retry tuning genuinely doesn't work (measured, not theoretical) - the lockstep-collision failure mode in the Context section is a real trap that cost real debugging time to find and fix; a poorly-tuned retry loop can silently under-perform (6/10 instead of 10/10) without ever raising an exception that points at the real cause

## Trade-off Analysis

Pessimistic locking's appeal — no wasted retry work — matters most when writes are expensive to redo or contention is so severe that optimistic retries would thrash indefinitely. Neither applies here: a debit/credit's staged work (bump `LastModifiedAtUtc`, add a `LedgerEntry`, enqueue an outbox event) is cheap to redo, and Day 28's measurement shows a correctly-tuned optimistic retry loop handles real 20-way contention at 100% of theoretical capacity. What optimistic concurrency buys instead — no blocking, no lock-hold-duration risk, no deadlock surface — matters more for a service explicitly designed to also participate in Phase 7's Payment saga (a slow downstream call during that saga must never be able to stall unrelated wallet writes by holding a lock open). The real cost of choosing optimistic concurrency wasn't in the concept, it was in getting the retry strategy right - and that cost is now paid and measured, not hypothetical.

## Consequences

- No explicit locking code exists anywhere in Wallet - the entire concurrency story lives in `LedgerWriter.ApplyAsync`'s retry loop, directly testable and already covered by `ConcurrentDebitTests`
- The jittered-backoff tuning (1-20ms, `MaxAttempts=25`) is empirically justified for this project's measured contention (20-way burst on one account), not a default copied from documentation - if a future scenario needs materially higher sustained contention on one account, these constants are the first thing to re-measure, not assume
- A writer that exhausts all 25 attempts still fails closed with `"Concurrent modification - retry."` rather than hanging - callers (Payment, in Phase 7) need their own outer retry/backoff or explicit failure handling for this case, since Wallet's internal retry is not infinite
- Revisit trigger: if a real access pattern emerges where one account is written far more frequently than this project's test scenario (e.g., a shared "house" account debited on every transaction platform-wide), that's the point to re-measure whether `MaxAttempts=25` with 1-20ms jitter still converges, or whether a hybrid (e.g. pessimistic locking only for that specific hot-account pattern) becomes justified

## Action Items

1. [x] Configure `Account.RowVersion` as Postgres `xmin` (`AccountConfiguration`, Day 23)
2. [x] Implement bounded, jittered-backoff retry in `LedgerWriter.ApplyAsync` (Day 28)
3. [x] Measure actual behavior under real contention rather than assuming it works (`ConcurrentDebitTests`, Days 27-28)
4. [ ] Re-measure `MaxAttempts`/jitter tuning if a materially different contention pattern emerges (see Consequences' revisit trigger) - not needed yet

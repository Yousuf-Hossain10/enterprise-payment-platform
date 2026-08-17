# ADR-0006: Immutable Ledger vs. Mutable Balance Column

**Status:** Accepted
**Date:** 2026-08-17
**Deciders:** Platform Architect (solo capstone)

## Context

Wallet is the highest-risk service in the platform — money must never be created, destroyed, or double-spent by a bug (`docs/Enterprise_Payment_Platform_Tutorial.md`, Phase 6). The core design question, decided at Day 23 and exercised heavily through Days 24-29, is how an account's balance is represented: as a mutable, directly-updated number, or as a computed value derived from an append-only history of every change.

What's implemented (`src/Services/Wallet/Wallet.Domain/Account.cs`, `LedgerEntry.cs`): `Account` has **no** `Balance` property at all. Every debit and credit is an immutable `LedgerEntry` row (`Amount` positive for credit, negative for debit), and the balance is always computed by summing a account's entries (`IAccountRepository.GetBalanceAsync`, `Wallet.Infrastructure/AccountRepository.cs`). This isn't a performance optimization or a stylistic choice — it's the thing that makes the rest of Phase 6's work (idempotency-key replay, the reconciliation report, Days 24-29) possible to build correctly at all.

The starter question this ADR answers (`ADR-Template-and-Starter-Log.md`): *walk through what happens to a mutable balance column under two concurrent debits without the ledger design.*

**The race, explicitly:** say `Account.Balance = 100`, and two debits of `60` each arrive concurrently.
1. Request A reads `Balance = 100` into memory.
2. Request B reads `Balance = 100` into memory (A hasn't written yet).
3. Both check `100 >= 60` — both pass the insufficient-funds guard.
4. Request A writes `Balance = 100 - 60 = 40`.
5. Request B writes `Balance = 100 - 60 = 40`.

Final balance: `40`. Correct answer: `100 - 60 - 60 = -20` (should have been rejected as insufficient funds, or at minimum only one should have succeeded). The account has been overdrawn by `60` with **no error, no exception, no trace of what happened** — the mutable column doesn't just risk losing track of an update, it silently converges to a *plausible-looking but wrong* number, and there is no way to tell from the data alone that this happened. This is exactly the failure mode `ConcurrentDebitTests` (Day 27) was written to catch, and did catch during Day 28's tuning (measured 6/10 successes under contention before the retry fix — a mutable-balance design wouldn't have surfaced that as 6 failures, it would have silently produced a wrong number with no failures at all).

## Decision

Use an immutable, append-only `LedgerEntry` per account as the sole source of truth. `Account` carries no `Balance` field; balance is always a computed sum, never a stored value that can go stale or be overwritten inconsistently.

## Options Considered

### Option A: Mutable `Balance` column
| Dimension | Assessment |
|-----------|------------|
| Complexity | Lowest at first glance — a single `UPDATE Accounts SET Balance = Balance - @amount` |
| Cost | Cheapest possible read (`SELECT Balance`) |
| Scalability | Read-cheap, but every write is a point of catastrophic risk without careful locking |
| Learning value | Low — actively teaches the wrong lesson for a financial system; the race condition above is exactly the kind of bug real production fintech incidents are made of |

**Pros:** trivial to read, no aggregation cost, matches how a beginner would naturally model "an account has a balance"
**Cons:** the race condition above is not a hypothetical edge case, it's the *default* behavior under any concurrent load without additional locking; no audit trail (once overwritten, the old value and the operations that produced the new one are gone); no way to reconcile or detect corruption after the fact, since the wrong number looks exactly like a right number

### Option B: Immutable ledger, computed balance (implemented)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — every balance read is an aggregation query, and the concurrency story shifts to "how do I know I'm not writing against a stale read" (answered by ADR-0007) |
| Cost | More expensive reads as entry count grows (unbounded `SUM` over a growing table) — a real concern deferred rather than solved here (see Consequences) |
| Scalability | Write-safe by construction; read cost is a known, addressable scaling problem (indexing, periodic snapshotting) rather than a silent correctness bug |
| Learning value | High — this is how real double-entry/ledger-based financial systems are built, and it's what makes Day 24-29's idempotency, concurrency-retry, and reconciliation work meaningful rather than cosmetic |

**Pros:** the race condition in the Context section is structurally impossible — two concurrent writes either both land as two entries (if funds allow) or one is rejected (RowVersion/xmin conflict, ADR-0007), never silently merged into a wrong number; full audit trail for free (every entry is permanent); the reconciliation report (Day 29) has something real to verify, since the "ground truth" and "computed value" are the same query executed two independent ways, not a cached value that could have drifted
**Cons:** balance queries get more expensive as `LedgerEntries` grows without bound; no compensating mechanism (snapshotting, materialized views) exists yet to bound that cost

## Trade-off Analysis

The deciding factor is what each design does under contention, not what it costs under a single, uncontended read. A mutable balance column's failure mode is silent and undetectable after the fact — the account converges to a wrong number that is indistinguishable from a right one, which is the worst possible failure mode for a financial ledger (worse than a loud crash, because nobody knows to look). An immutable ledger's failure mode under the same contention is a *rejected write* (a `ConcurrencyConflictException` the caller sees and can retry) or a correctly-serialized pair of writes — never data corruption that hides itself. Given this project's explicit goal ("money can never be created, destroyed, or double-spent by a bug"), the read-cost trade-off of Option B is the correct price to pay; a cheap read that can silently lie is not a real savings.

## Consequences

- Every balance query is an aggregation over `LedgerEntries`, not a single-row lookup — acceptable at this project's scale, but a real future scaling concern once an account accumulates a large transaction history
- Full, permanent audit trail exists by construction — no separate audit mechanism was needed to answer "what happened to this account's balance," which is exactly the property Phase 9 (Audit Service, append-only tamper-evident log) extends platform-wide
- The reconciliation report (Day 29) is meaningful specifically because there's a real independent-computation-path check to run (LINQ sum vs. raw SQL sum) rather than "does the cache match the source" — a mutable-balance design would have made that report a no-op (the cache *is* the source)
- Revisit trigger: if unbounded `LedgerEntries` growth ever makes `GetBalanceAsync` a measurable performance problem, the fix is a periodic, auditable balance *snapshot* (itself an immutable ledger entry type, e.g. `BalanceSnapshot` with a "sum from here forward" semantic) — not reintroducing a directly-mutable `Balance` column, which would reopen the exact race this ADR exists to close

## Action Items

1. [x] Implement `Account`/`LedgerEntry` with no mutable balance field (`Wallet.Domain`, Day 23)
2. [x] Implement `GetBalanceAsync` as a computed sum (`AccountRepository`, Day 23-24)
3. [x] Prove the design holds under real concurrent load (`ConcurrentDebitTests`, Days 27-28)
4. [ ] Balance-snapshotting for read-cost scaling - not needed at current scale, tracked as a future revisit trigger above, not implemented

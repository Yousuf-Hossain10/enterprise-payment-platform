# Learning Journal — Enterprise Payment Platform

*Companion to the tutorial and ADR log. This is where the capstone becomes a documented journey instead of just a finished repo — the journal is often more valuable to a hiring conversation than the code itself, because it shows how you think.*

Keep this as `docs/journal/` — one entry file per session (`2026-07-28-phase-3-bootstrap.md`) or one running file, whichever you'll actually maintain. Consistency matters more than format purity.

---

## Entry Template

```markdown
## [Date] — Phase [N]: [What you worked on]

**Time spent:** [rough hours]
**Status:** In progress / Blocked / Completed this session

### What I built
[2-4 sentences, plain language, no jargon you can't yet explain]

### The core concept today
[Name the one idea that mattered most today — e.g. "optimistic concurrency control"]

### Explain it like I'm teaching a junior dev
[Write 3-5 sentences as if explaining this concept to someone who's never
heard of it. If you can't do this without hedging or hand-waving, you don't
understand it yet — that's useful information, not a failure. Go back and
re-read/re-test until you can.]

### Where I got stuck
[Be specific. "Concurrency was confusing" is not useful to future-you.
"I didn't understand why EF Core's RowVersion throws on SaveChanges
instead of on the query" is useful.]

### How I resolved it
[What fixed your understanding — a specific test you wrote, a diagram you
drew, a doc you read, a conversation. If unresolved, say so and carry it
to the next entry.]

### Decisions made
[Link to any ADR you wrote today, or note "none — implementation only"]

### Resources used
[Docs, articles, books, Stack Overflow threads — whatever actually helped]

### Next question to explore
[One thing you're curious about that's outside today's scope — a running
"someday" list. Some of these become Phase 18+ stretch goals.]
```

---

## Example Entry (filled in, to show the depth you're aiming for)

```markdown
## 2026-08-03 — Phase 6: Wallet ledger design

**Time spent:** ~3 hours
**Status:** Completed this session

### What I built
Implemented the LedgerEntry/Account tables and the DebitAsync method with
optimistic concurrency via RowVersion. Wrote a test that fires 20 parallel
debit requests against an account with just enough balance for 1.

### The core concept today
Optimistic concurrency control — assume conflicts are rare, detect them
at write time instead of locking at read time.

### Explain it like I'm teaching a junior dev
Imagine two people trying to spend the last $10 in a shared account at the
same time. Pessimistic concurrency is like making everyone stand in line
and lock the account while they check the balance — safe but slow.
Optimistic concurrency lets both people read the balance simultaneously,
but the database attaches a version stamp to the row. When either person
tries to save, the database checks "is this still the version I read?" —
if someone else already spent the money and the version changed, the
second write fails and has to retry. It trades a small chance of wasted
work (a retry) for much higher throughput in the common case where
conflicts are rare.

### Where I got stuck
I initially expected the conflict to show up as an exception on the
*query* (reading the account), but it actually only throws on
SaveChangesAsync — the conflict is detected at write time, not read time.
That's the whole point of "optimistic," but it wasn't intuitive until I
saw the DbUpdateConcurrencyException in the debugger.

### How I resolved it
Set a breakpoint on SaveChangesAsync during the 20-parallel-request test
and watched exactly which calls threw and when. Also re-read the EF Core
docs section on concurrency tokens.

### Decisions made
ADR-0007 — optimistic vs. pessimistic concurrency control (see ADR log)

### Resources used
Microsoft EF Core docs — "Handling concurrency conflicts"; re-read the
Wallet section of my own tutorial

### Next question to explore
How does this change under very high contention (thousands of concurrent
writers to one account)? Is there a point where pessimistic locking or a
different data model (e.g. sharding by account) becomes necessary? Worth
a stretch-goal load test later.
```

---

## Running "Concepts Mastered" Checklist

Check these off as you can genuinely pass the "explain it to a junior dev" test — not just when the code compiles. Add rows as you discover more concepts; this list (drawn from the Concept Study Guide) is a starting point.

| Phase | Concept | Can explain simply? | Journal entry link |
|---|---|---|---|
| 1 | C4 model / architecture diagramming | ☐ | |
| 1 | ADRs as a decision-making practice | ☐ | |
| 4 | Outbox pattern | ☐ | |
| 4 | Idempotent consumers | ☐ | |
| 4 | Correlation ID propagation | ☐ | |
| 5 | AuthN vs AuthZ | ☐ | |
| 5 | JWT structure & claims | ☐ | |
| 5 | Refresh token rotation & theft detection | ☐ | |
| 6 | ACID properties | ☐ | |
| 6 | Optimistic vs pessimistic concurrency | ☐ | |
| 6 | Double-entry bookkeeping | ☐ | |
| 7 | Saga pattern (orchestration vs choreography) | ☐ | |
| 7 | Circuit breaker / retry / bulkhead patterns | ☐ | |
| 8 | Message delivery semantics (at-least-once, etc.) | ☐ | |
| 9 | Event sourcing vs audit logging | ☐ | |
| 10 | BFF pattern | ☐ | |
| 12 | Readiness vs liveness probes | ☐ | |
| 12 | NetworkPolicy default-deny model | ☐ | |
| 14 | Deployment strategies (rolling/blue-green/canary) | ☐ | |
| 15 | Three pillars of observability | ☐ | |
| 15 | SLI / SLO / error budgets | ☐ | |
| 16 | STRIDE threat modeling | ☐ | |
| 17 | Test pyramid / contract testing | ☐ | |
| 17 | Chaos engineering principles | ☐ | |
| 18 | Rules-engine / weighted scoring design | ☐ | |
| 18 | Fail-open vs fail-closed under dependency failure | ☐ | |
| 19 | CQRS (Command Query Responsibility Segregation) | ☐ | |
| 19 | Eventual consistency & staleness windows | ☐ | |
| 19 | Rebuild-from-event-log as a correctness proof | ☐ | |

*(Full concept list with resources lives in `Concept-Study-Guide.md`.)*

---

## Turning Entries Into a Portfolio

You don't need to publish every entry, but consider turning your strongest 4–6 into standalone blog posts or a personal wiki once a phase is done — these are the ones that read like real engineering writing, not a diary:
- The Wallet concurrency post (Phase 6) — ledger design + the parallel-debit test
- The Payment saga post (Phase 7) — orchestration choice + fault-injection test
- The observability post (Phase 15) — the single-trace-across-five-services moment
- The incident/postmortem you'll eventually write in Phase 17's chaos test, treated as a real postmortem

A senior engineer's portfolio is judged less by "did they build it" and more by "can they explain why it's built this way, and what they'd change." That's exactly what this journal is training.

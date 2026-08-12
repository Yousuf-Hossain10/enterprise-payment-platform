# Concept Study Guide — Enterprise Payment Platform

*Companion to the tutorial, ADR log, and learning journal. This maps every phase to the underlying concepts worth mastering — not the implementation steps (that's the tutorial), but the theory a senior engineer is expected to reason from. For each phase: what to study, why it matters at a senior level, and a question to test whether you actually understand it.*

Use this alongside the Learning Journal — the "explain it like I'm teaching a junior dev" exercise there is how you verify you've actually internalized what's listed here, not just read about it.

---

### Phase 1 — Architecture & Planning
**Concepts:** the C4 model (context/container/component/code), sequence diagrams as a design tool (not just documentation), ADRs as a decision-making discipline.
**Why it matters:** senior engineers are judged heavily on how they communicate design *before* code exists. A design review where you can produce the right diagram for the right audience (executives want context diagrams, engineers want component diagrams) is a real differentiator.
**Test yourself:** Could you draw the container diagram for this platform from memory, on a whiteboard, in under 5 minutes?

### Phase 4 — Shared Foundation Libraries
**Concepts:** the outbox pattern (and why "publish then save" is a correctness bug), idempotent consumers, distributed context propagation (W3C Trace Context / correlation IDs), the Options pattern for configuration.
**Why it matters:** the outbox pattern is one of the most commonly *misunderstood* distributed systems patterns — people build "dual writes" (save to DB, then publish to broker) without realizing the two can partially fail independently.
**Test yourself:** Explain exactly what breaks if you publish the event first and the DB write fails second. Then explain what breaks if you reverse the order without an outbox.

### Phase 5 — Identity Service
**Concepts:** authentication vs. authorization, JWT structure (header/payload/signature) and why claims shouldn't contain sensitive data, password hashing (why memory-hard algorithms like Argon2id resist GPU cracking better than SHA-256), refresh token rotation and reuse detection.
**Why it matters:** auth is the subsystem where subtle mistakes have outsized consequences, and it's disproportionately represented in real security incidents.
**Test yourself:** If someone steals a valid refresh token, what specifically in your rotation design detects and stops them?

### Phase 6 — Wallet Service
**Concepts:** ACID properties, transaction isolation levels (and what each one actually prevents — dirty read, non-repeatable read, phantom read), optimistic vs. pessimistic concurrency control, double-entry bookkeeping as an accounting concept (not just a DB schema trick).
**Why it matters:** this is the closest thing in the project to "money-grade" correctness reasoning — the kind of thinking that separates engineers who can be trusted near financial systems from those who can't yet.
**Test yourself:** Why does an immutable ledger make reconciliation trivial, while a mutable balance column makes it nearly impossible to prove correctness after the fact?

### Phase 7 — Payment Service
**Concepts:** distributed transactions and why two-phase commit doesn't scale well across services, the Saga pattern (orchestration vs. choreography), circuit breaker / retry / bulkhead patterns (from Michael Nygard's *Release It!*), idempotency keys as a client-facing correctness contract.
**Why it matters:** sagas are the pattern every growing microservices system eventually needs and very few engineers have implemented and tested properly, as opposed to just read about.
**Test yourself:** In your saga, what exact state does a payment end up in if the process crashes between the Wallet debit succeeding and the outbox event being written? Is that acceptable, and why?

### Phase 8 — Notification Service
**Concepts:** message delivery semantics (at-most-once vs. at-least-once vs. exactly-once — and why true exactly-once across a network is effectively a myth), deduplication strategies, dead-letter queues as a safety valve.
**Why it matters:** almost every real distributed system is at-least-once under the hood; understanding why "exactly-once" claims from vendors are usually "effectively-once via dedup" is a mark of hard-won experience.
**Test yourself:** Your consumer processes an event, then crashes before acking it back to RabbitMQ. What happens next, and why doesn't it cause a duplicate notification?

### Phase 9 — Audit Service
**Concepts:** event sourcing vs. traditional audit logging (they're often confused — event sourcing derives current state *from* the log; audit logging is a side record next to normal CRUD state), immutability guarantees, tamper-evidence via hash chaining.
**Why it matters:** understanding where your design sits on the event-sourcing spectrum clarifies what guarantees you actually have versus what you assume you have.
**Test yourself:** If someone with direct DB access modified a ledger entry in Wallet, would your Audit service's hash chain detect it? Walk through exactly how.

### Phase 10 — Angular Frontend
**Concepts:** the Backend-for-Frontend (BFF) pattern, token refresh flows and their failure modes, reactive vs. imperative state management trade-offs.
**Why it matters:** frontend architecture decisions age differently than backend ones — a BFF choice you make here affects every future client (mobile app, third-party integration) you might add.
**Test yourself:** What breaks for a future mobile client if the frontend currently calls services directly instead of through the Gateway?

### Phase 12 — Kubernetes Manifests
**Concepts:** readiness vs. liveness probes (and the outage pattern caused by conflating them), resource requests/limits and Kubernetes QoS classes, the NetworkPolicy default-deny model, HPA scaling mechanics.
**Why it matters:** most Kubernetes production incidents trace back to misunderstanding one of these four things, not to Kubernetes itself being unreliable.
**Test yourself:** What happens to traffic routing if your readiness probe is misconfigured to always return 200, even while the app is unable to reach Postgres?

### Phase 14 — GitHub Actions CI/CD
**Concepts:** deployment strategies (rolling, blue-green, canary) and their different risk/rollback profiles, the idea of a "quality gate" as an enforced, non-optional check.
**Why it matters:** CI/CD design reflects an organization's actual risk tolerance — a senior engineer can explain the trade-off, not just configure the YAML.
**Test yourself:** Why does a rolling update not fully protect you from a bad deploy the way a canary strategy does, even though both avoid full downtime?

### Phase 15 — Observability Stack
**Concepts:** the three pillars (logs, metrics, traces) and what each is and isn't good for, the RED method (Rate/Errors/Duration) vs. USE method (Utilization/Saturation/Errors), SLIs/SLOs/error budgets as a way to make reliability a measurable, negotiable target instead of a vague goal.
**Why it matters:** "observability" is one of the most overused words in the industry; being able to precisely say what a trace tells you that a log can't (and vice versa) is what separates real fluency from buzzword familiarity.
**Test yourself:** A customer reports a slow payment. Walk through exactly which tool (logs, metrics, or traces) you'd open first, second, and third, and why in that order.

### Phase 16 — Security Hardening
**Concepts:** STRIDE threat modeling (Spoofing/Tampering/Repudiation/Information disclosure/Denial of service/Elevation of privilege), defense in depth, least privilege at the network and IAM level, software supply chain security (SBOMs, dependency scanning).
**Why it matters:** security thinking that starts with "what can go wrong, systematically" rather than "did we remember to add auth" is a structural difference between junior and senior approaches to the same problem.
**Test yourself:** Pick one STRIDE category and name a concrete threat in your Wallet service that it surfaces which the others wouldn't.

### Phase 17 — Comprehensive Testing
**Concepts:** the test pyramid (and why it's a pyramid, not a rectangle), consumer-driven contract testing, chaos engineering's core principle (verify hypotheses about system behavior under real, injected failure rather than assuming resilience).
**Why it matters:** chaos engineering in particular is a mindset most engineers only adopt after being burned by an incident that "should have been caught" — doing it deliberately here is a shortcut past that scar tissue.
**Test yourself:** What specific hypothesis is your chaos test (killing a Wallet pod under load) actually verifying, in one sentence?

---

### Phase 18 — Fraud/Risk Service *(extension)*
**Concepts:** rules-engine design (weighted scoring vs. hard gates), the fail-open/fail-closed trade-off under dependency failure, human-in-the-loop workflows for ambiguous cases.
**Why it matters:** fail-open/fail-closed is a business-risk decision disguised as an engineering one — being able to frame it that way in a conversation with a non-engineer is a senior-level communication skill, not just a technical one.
**Test yourself:** Argue convincingly for the *opposite* of whatever failure mode you chose in ADR-0018. If you can't, you may not have stress-tested your own reasoning enough.

### Phase 19 — Reporting/Analytics Service *(extension)*
**Concepts:** CQRS (Command Query Responsibility Segregation), eventual consistency and staleness windows, rebuild-from-event-log as a correctness proof.
**Why it matters:** CQRS is one of the most frequently name-dropped and least frequently *implemented* patterns in interviews — having actually built and rebuilt one gives you a real answer instead of a textbook one.
**Test yourself:** Why is it safe for the Reporting service's database to be fully disposable, while the Wallet service's database absolutely cannot be?

## Reference Books (general, not time-sensitive — worth owning, not just skimming)

- *Designing Data-Intensive Applications* — Martin Kleppmann (the single best book for Phases 6–9's data/consistency concepts)
- *Release It!* — Michael Nygard (Phase 7's circuit breaker/bulkhead patterns, straight from the source)
- *Building Microservices* — Sam Newman (service boundaries, Phase 1 and 9 especially)
- *Kubernetes Patterns* — Bilgin Ibryam & Roland Huß (Phases 12–13)
- *Site Reliability Engineering* and *The SRE Workbook* — Google (free online; Phase 15's SLO/error-budget concepts come straight from here)
- *Accelerate* — Nicole Forsgren, Jez Humble, Gene Kim (the research behind why Phase 14's CI/CD practices matter, not just how to configure them)
- OWASP ASVS and OWASP Top 10 (official, freely available — Phase 16)

Don't try to read these cover-to-cover before continuing the project — pull the relevant chapter when you hit the phase it maps to. Concepts learned right before you need them stick better than concepts learned in the abstract.

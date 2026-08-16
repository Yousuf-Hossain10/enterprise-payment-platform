# Security Model

This is the outline defined in `Phase4-17_Breakdown_and_Security_Model.md`, populated with what's actually decided as of Phase 1. Several sections are deliberately thin here and get filled in by the phase that implements them (noted per section) — this file is written once at Phase 1 and kept living, not written once and frozen.

## 1. Identity & Access

- **Authentication flow:** username/password login issuing a JWT access token + refresh token pair (Identity Service, Phase 5). No third-party OIDC provider — Identity *is* the authorization server for this platform, which is a deliberate scope reduction appropriate for a simulation rather than a real multi-tenant system.
- **Role/claims model:** JWT carries role and permission claims; authorization is enforced via the `[RequirePermission("wallet:debit")]`-style attribute from `BuildingBlocks.Security` (`Coding-Standards.md`), not ad hoc role string checks in handlers.
- **Service-to-service auth:** every service validates the same JWT issued by Identity (shared signing key/JWKS) rather than services re-authenticating to each other independently. Whether internal calls (e.g. Payment → Wallet) also require the caller's JWT to be forwarded, versus a separate service-identity credential, is decided in Phase 5/7 as those services are built — flagged here as an open question, not yet an ADR-worthy one on its own.

## 2. Token Lifecycle

- Access token TTL and refresh token rotation/revocation strategy are decided as ADR-0005 (JWT vs. opaque tokens + introspection) in Phase 5 — not finalized yet, per `Technology-Decisions.md`'s ADR index.
- Whatever the TTL, refresh tokens rotate on use (a used refresh token is immediately invalidated and replaced) and are revocable server-side (`RefreshToken.Revoked` flag, per the domain model sketched in the tutorial's Phase 5 section) — this part is fixed regardless of the ADR-0005 outcome, since it's a baseline expectation, not a trade-off.
- Signing keys do not live in `appsettings.json` or source control — local dev sources them from a K8s Secret (Phase 3); where a real system would keep them (Vault, a cloud KMS) is noted as a "how this would differ in production" callout when Phase 16 is reached, not implemented here.

## 3. Secrets Management

- **Local/dev:** plain Kubernetes Secrets, provisioned by `scripts/bootstrap.sh`/`.ps1` (Phase 3, Day 8). This is an accepted risk at this project stage — the local cluster (Docker Desktop Kubernetes, see `Deployment-Strategy.md`) is single-user with no external exposure.
- **What a real production system would do instead:** external-secrets or Vault, so secrets aren't base64-in-etcd. This is captured as ADR-0012 (Phase 12) rather than implemented, since standing up Vault for a solo local-only project would be infrastructure cosplay without a real secret-rotation need to justify it — the ADR is where that reasoning gets written up properly.
- CI/CD secret handling (how GitHub Actions accesses deploy credentials without printing them to logs) is defined in Phase 14 once the pipeline exists.

## 4. Network Security

- NetworkPolicies (Phase 12, Day 61) enforce default-deny between namespaces/pods, with explicit allow rules per legitimate call path (e.g. Gateway → each service, Payment → Wallet, every service → its own Postgres/RabbitMQ). No service can reach another service it has no documented reason to call.
- Ingress TLS termination happens at the NGINX Ingress controller (Phase 3); internal cluster traffic is plaintext HTTP for now — mTLS between services is an explicit, documented deferral (ADR-0016, Phase 16), not an oversight, since NetworkPolicies already provide meaningful isolation without the operational cost of a service mesh at this scale.

## 5. Data Protection

- Postgres encryption at rest is deferred to whatever the underlying storage layer provides (Docker Desktop Kubernetes' default StorageClass has no encryption-at-rest guarantee) — noted explicitly as a simulation limitation, not something this project claims to solve.
- **This platform simulates payment data; it does not process real payment data.** No real cardholder data, no real PCI-DSS scope. Amounts, account identifiers, and "payments" are internal-ledger constructs only — this is stated here explicitly so the security posture is never misread as a claim of real financial-system compliance.
- PII is limited to what Identity stores (email, password hash) — no additional PII fields are planned elsewhere in the system as currently scoped.

## 6. Application Security Controls

- Input validation via FluentValidation base validators (`BuildingBlocks.Common`, Phase 4) on every command/query DTO — validation happens at the Application boundary, not scattered in controllers.
- Rate limiting at the Gateway on sensitive endpoints (login, payment creation) — implemented in Phase 16 (Day 81), specified here as a requirement so it isn't dropped.
- Idempotency-key enforcement on financial endpoints — see `API-Guidelines.md`.
- Dependency scanning (Dependabot/Snyk) and secrets scanning (gitleaks) run in CI from Phase 16 (Day 80) onward.

## 7. Threat Modeling

- Full STRIDE threat models for Wallet, Payment, and Identity are written in Phase 16 (Day 79) — these are the three highest-value targets (money movement and the credentials that unlock it), so they're modeled first and in most depth.
- Notification and Audit get lighter-weight review at the same time, since their blast radius on compromise is materially lower (no money movement, no credential storage).

### 7.1 Identity Service — early threat notes (Phase 5)

These two threats are addressed now, ahead of the full Phase 16 STRIDE pass, because Identity issues the credential every other service trusts — leaving them unmitigated until Day 79 would mean weeks of the platform running with no defense at all.

- **Brute-force credential guessing (Spoofing).** `POST /api/v1/auth/login` is rate-limited per client IP: 5 attempts per rolling 1-minute window (`Identity.Api/LoginRateLimiting.cs`), fixed-window, no queueing — the 6th attempt in a window gets `429` immediately rather than waiting. This is a service-level stopgap: the Gateway doesn't exist yet (Phase 10) and its own rate limiting on sensitive endpoints lands Phase 16 (Day 81); this policy is the interim defense-in-depth layer and stays in place as a second layer once the Gateway's exists too. Per-IP partitioning is a known-imperfect mitigation (shared NAT/proxy IPs, IPv6 rotation) — a stronger identity-aware limiter (per-account, with backoff) is left as a Phase 16 refinement rather than solved here, since the login handler already returns the same generic "Invalid email or password" error for both unknown-email and wrong-password cases, which removes the cheaper enumeration attack this would otherwise also need to cover.
- **Refresh token theft (Spoofing / Elevation of Privilege).** Refresh tokens are opaque, high-entropy (64 random bytes, base64url), and stored server-side only as a SHA-256 hash — a database read alone (backup leak, read replica compromise) does not yield a usable token. Rotation-on-use means a stolen-but-unused token is invalidated the moment the legitimate client next refreshes, and the replaced-token chain (`ReplacedByTokenHash`) means a captured *and* replayed old token, after the legitimate client has already rotated past it, is detectable as reuse rather than silently accepted — see `RefreshCommandHandler`. Logout (`RevokeCommandHandler`) gives an explicit server-side kill switch independent of expiry. What's *not* yet built: automatic revoke-entire-chain-on-reuse-detected (currently reuse of an already-rotated token just fails closed as "invalid," it doesn't proactively revoke the rest of that user's active tokens) — flagged as a Phase 16 hardening item, not a Phase 5 gap, since the baseline (fail closed) is already safe, just not maximally responsive.

## 8. Security Testing

- OWASP ZAP baseline scan against the running platform, Phase 16 (Day 82). Findings are triaged in the same day's ADR/journal entry; no unresolved high-severity findings is the Definition of Done bar (per `Phase4-17_Breakdown_and_Security_Model.md`).
- Cadence beyond the one-time Phase 16 scan (e.g. re-running before considering the project "done" in Phase 17) is noted as a to-do rather than committed to a fixed schedule, since this is a capstone, not an operated production system with an ongoing release cadence.

## 9. Incident Response

- In a solo/simulated context, "incident response" means: the project owner plays every role (detector, responder, postmortem author). This is stated explicitly rather than pretending an on-call rotation exists.
- A written incident-response runbook is produced in Phase 17 (Day 88) and dry-run at least once before being considered done, per that phase's Definition of Done.
- Until Phase 17, if something resembling a real security issue is found during development (e.g. a secret accidentally committed), it's treated seriously in the moment — fixed, rotated if it was a real credential, and noted in that day's Learning Journal entry — rather than deferred to "the security phase."

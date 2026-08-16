# ADR-0005: JWT vs. Opaque Tokens + Introspection

**Status:** Accepted
**Date:** 2026-08-17
**Deciders:** Platform Architect (solo capstone)

## Context

Every other service on the platform trusts a token Identity issues — this is the credential that gates every subsequent request, per `docs/Microservice-Responsibilities.md`'s note that Identity is "implicitly trusted by every other service via JWT validation... rather than a live call per request." Two questions were actually in play: what format the *access* token takes, and what format the *refresh* token takes — and the implementation answers them differently.

What's live today (`src/Services/Identity/Identity.Application/ITokenService.cs`, `TokenPair.cs`; `src/Services/Identity/Identity.Infrastructure/JwtTokenService.cs`):

- **Access token:** a self-contained JWT (`JwtTokenService`, `BuildingBlocks.Security.JwtOptions`), claims `sub`, `email`, `jti`, plus one `ClaimTypes.Role` and one `permission` claim per entry in `User.Roles`. Default lifetime 15 minutes (`TokenIssuanceOptions.AccessTokenLifetime`). Every service validates it locally against the shared signing key — no call back to Identity per request.
- **Refresh token:** an opaque, high-entropy random value (`RandomNumberGenerator.GetBytes(64)`, base64url-encoded) — carries no claims, is meaningless outside Identity. Stored server-side only as a SHA-256 hash (`Sha256RefreshTokenHasher`, `RefreshToken.TokenHash`), never in plaintext. Default lifetime 14 days. Rotates on every use (`RefreshCommandHandler` — old token revoked and chained via `ReplacedByTokenHash`, new token issued, both writes atomic in one `SaveChangesAsync`) and is explicitly revocable server-side (`RevokeCommandHandler`, i.e. logout).

So the actual decision already made is a **hybrid**: self-contained JWT for the short-lived access token (optimizing for per-request validation cost — no round trip to Identity on every API call across every service), opaque + server-side-hashed for the long-lived refresh token (optimizing for revocation control, since a refresh token is what an attacker would most want to steal and reuse over an extended window). This ADR is the formal write-up per `ADR-Template-and-Starter-Log.md`'s framing for ADR-0005 (Phase 5): *"What do you give up in revocation control by choosing self-contained JWTs?"* — the question the hybrid design is explicitly answering by *not* making the long-lived token self-contained.

Related, already-fixed-regardless-of-this-ADR per `docs/Security-Model.md` §2: refresh token rotation-on-use and server-side revocability are treated as a non-negotiable baseline, not something this ADR's outcome could trade away.

## Decision

Use a hybrid: a short-lived, self-contained JWT for the access token (15-minute default) and an opaque, server-side-hashed, rotating refresh token (14-day default) for session continuance — the implementation already in place in `JwtTokenService`, `Sha256RefreshTokenHasher`, `RefreshCommandHandler`, and `RevokeCommandHandler`.

## Options Considered

### Option A: Pure JWT (self-contained access *and* refresh tokens)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low — one token format everywhere, no server-side token storage needed for either token type |
| Cost | Lowest infrastructure cost — no refresh-token table, no per-refresh DB round trip |
| Scalability | Best — fully stateless validation for both token types, trivially horizontally scalable |
| Learning value | Lower for this specific project — sidesteps the revocation problem entirely rather than solving it, and revocation/rotation is exactly what Phase 5's Definition of Done calls out |

**Pros:** no database dependency for token validation at all, simplest to implement, best raw scalability
**Cons:** revocation is fundamentally hard — a stolen or leaked long-lived JWT stays valid until natural expiry no matter what the server does, short of maintaining a denylist (which reintroduces the statefulness this option is supposed to avoid)

### Option B: Pure opaque tokens + introspection endpoint (access *and* refresh)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium-high — every service needs to call Identity's introspection endpoint (or a shared cache of it) to validate *every* request, not just refresh calls |
| Cost | Highest — a network/DB round trip per API call across every service, or a caching layer to compensate |
| Scalability | Worst without additional infrastructure (e.g. a shared Redis cache of introspection results) — Identity becomes a request-path dependency for every service, every request |
| Learning value | High for revocation control, but the "every request round-trips to Identity" cost is a real production concern that doesn't have a small-scale escape hatch the way it does for the hybrid |

**Pros:** full, immediate revocation control over every token including access tokens, no risk of a stale JWT outliving a revoked session
**Cons:** turns Identity into a synchronous dependency for every request on the platform (a Wallet or Payment call now depends on Identity's uptime, not just its historical signing key), materially higher latency and infrastructure cost per request

### Option C: Hybrid — JWT access token + opaque refresh token (implemented)
| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium — two token formats, one piece of server-side state (`RefreshToken` table) instead of zero or "everything" |
| Cost | Low-medium — refresh tokens are used rarely (every ~15 min at most, per `AccessTokenLifetime`) relative to access-token validation, which is stateless and free |
| Scalability | Good — the expensive-to-scale operation (per-request validation) is stateless; the stateful operation (refresh) is infrequent by construction |
| Learning value | High — this is the actual industry-standard pattern (OAuth2 access/refresh split), and building it surfaces the real trade-off: short-lived self-contained tokens bound the blast radius of "can't revoke a JWT," rather than eliminating it |

**Pros:** stateless per-request validation at scale (the common case), real revocation control where it matters most (a long-lived, high-value refresh token), rotation-on-use limits a stolen-but-unused refresh token's window, explicit logout works
**Cons:** a stolen access token is still valid for up to its full lifetime (bounded to 15 minutes here, not zero) — this is an accepted, bounded risk rather than a solved one; two token formats to reason about instead of one

## Trade-off Analysis

The core question this ADR answers is what's given up in revocation control by choosing self-contained JWTs — the answer is: nothing, if JWTs are only used for the *access* token and kept short-lived. A self-contained JWT can't be revoked mid-flight (there's no server-side state to delete — that's the whole point of self-contained), so any use of JWT necessarily accepts "a stolen token stays valid until it expires, no matter what the server does in the meantime." The hybrid design's answer is to make that window as small as reasonably possible (15 minutes) rather than trying to eliminate it, and to spend the *real* revocation control on the token that actually matters for session-hijacking risk: the refresh token, which is long-lived (14 days) and is what an attacker would need to maintain persistent access. Making the refresh token opaque and server-side-hashed means Identity has full authority over it — revoke on logout, rotate on every use, detect reuse-of-an-already-rotated-token as a signal something's wrong (see `docs/Security-Model.md` §7.1).

The alternative extremes both make a real trade worse in one direction: pure JWT (Option A) pushes the "can't revoke" problem onto the token an attacker most wants to steal (the long-lived one), while pure opaque+introspection (Option B) makes every service's every request depend synchronously on Identity's availability and adds a network/DB round trip to the platform's hottest code path (request authorization) — a cost that would compound across every one of the five services on every request, not just at login/refresh time. The hybrid confines the "trust Identity is up" dependency to the comparatively rare refresh operation (at most once per 15-minute access-token lifetime) while keeping the frequent operation (per-request validation) fully stateless.

## Consequences

- A stolen access token remains usable for up to 15 minutes even after the user logs out or the refresh token is revoked — an accepted, bounded risk, not a solved one; shortening `AccessTokenLifetime` further would shrink this window at the cost of more frequent refresh calls
- Every service validates access tokens locally against the shared JWT signing key with no call back to Identity, which is what makes the platform's normal request path fast and keeps Identity off the critical path for everything except login/refresh/logout
- The `RefreshToken` table (`TokenHash`, `Revoked`, `ReplacedByTokenHash`) is the one piece of server-side session state this design requires — a real but small and well-scoped cost compared to Option B's "every access token needs server-side state too"
- Revoke-entire-chain-on-reuse-detected (i.e., if a rotated-away refresh token is presented again, proactively revoke every token descended from it, not just reject the one reuse attempt) is *not* implemented yet — currently reuse just fails closed as "invalid," which is safe but not maximally responsive. Flagged in `docs/Security-Model.md` §7.1 as a Phase 16 hardening item, not a Phase 5 gap, since the baseline is already safe
- Revisit trigger: if a future requirement needed *immediate* platform-wide revocation of an access token (not just the refresh token), that would call for either much shorter access-token lifetimes, a denylist/introspection layer for access tokens specifically, or reconsidering this ADR entirely — none of which is a current requirement

## Action Items

1. [x] Implement `JwtTokenService` (self-contained access token, `BuildingBlocks.Security.JwtOptions`)
2. [x] Implement opaque refresh tokens hashed at rest (`Sha256RefreshTokenHasher`, `RefreshTokenRepository`)
3. [x] Implement rotation-on-use and explicit revocation (`RefreshCommandHandler`, `RevokeCommandHandler`)
4. [ ] Implement revoke-entire-chain-on-reuse-detected (Phase 16 hardening item, per `docs/Security-Model.md` §7.1)

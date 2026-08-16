# ADR-0004: Password Hashing Algorithm

**Status:** Accepted
**Date:** 2026-08-17
**Deciders:** Platform Architect (solo capstone)

## Context

Identity stores one credential per user: `User.PasswordHash` (`src/Services/Identity/Identity.Domain/User.cs`). Whatever hits that column has to survive an offline attack against a leaked database dump, not just resist being reversed in normal operation — a raw SHA-256 hash of a password is fast to compute, which is exactly the property that makes GPU/ASIC brute-forcing practical against it (billions of guesses/sec on commodity hardware). The algorithm needs to be deliberately slow and, ideally, resistant to hardware acceleration.

`Argon2idPasswordHasher` (`src/Services/Identity/Identity.Infrastructure/Argon2idPasswordHasher.cs`, via `Konscious.Security.Cryptography.Argon2`) is already implemented and live — registered in `Program.cs`, exercised by `RegisterUserCommandHandler`/`LoginCommandHandler`, and covered by both unit tests and the Testcontainers integration suite. Current tuning:

| Parameter | Value |
|---|---|
| Salt | 16 bytes, `RandomNumberGenerator`-generated per hash |
| Output hash length | 32 bytes |
| Iterations (time cost) | 4 |
| Memory | 65,536 KB (64 MB) |
| Degree of parallelism | 2 |

Parameters are stored alongside the hash itself (`{iterations}.{memoryKb}.{parallelism}.{salt}.{hash}`), so a future tuning change doesn't invalidate already-issued hashes — `Verify` re-derives using whatever parameters are embedded in the stored value, not the current defaults. Verification uses `CryptographicOperations.FixedTimeEquals` for constant-time comparison.

This ADR is the formal write-up of a decision the code already reflects — per `ADR-Template-and-Starter-Log.md`'s framing for ADR-0004 (Phase 5): *"Why is SHA-256 alone wrong here, and what specifically does a memory-hard algorithm defend against?"*

## Decision

Use Argon2id for password hashing, with the tuning parameters already implemented in `Argon2idPasswordHasher` (4 iterations, 64 MB memory, parallelism 2), stored alongside each hash so future tuning changes don't invalidate existing credentials.

## Options Considered

### Option A: Argon2id
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low-medium — one extra NuGet package (`Konscious.Security.Cryptography.Argon2`), no built-in .NET BCL support |
| Cost | N/A (CPU/memory cost is the point — tunable via iterations/memory/parallelism) |
| Scalability | Tunable independently of login volume; memory-hardness is deliberately expensive to parallelize on GPU/ASIC |
| Learning value | High — Argon2id (2015 Password Hashing Competition winner) is the current OWASP-recommended default and combines Argon2i's side-channel resistance with Argon2d's GPU-cracking resistance |

**Pros:** memory-hard (defeats cheap GPU/ASIC parallelization in a way CPU-only algorithms can't), tunable time/memory/parallelism cost, side-channel-resistant hybrid mode, current OWASP #1 recommendation
**Cons:** no .NET BCL implementation (third-party package dependency), memory cost has real server-side resource implications under high concurrent login load, newer/less battle-tested in production than bcrypt

### Option B: bcrypt
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low — mature, widely available .NET packages (e.g. `BCrypt.Net-Next`) |
| Cost | N/A |
| Scalability | Work factor is tunable, but not memory-hard |
| Learning value | Medium — teaches adaptive hashing and work factors, but not the memory-hardness concept that's the more current defense against purpose-built cracking hardware |

**Pros:** decades of production track record, simple API, tunable cost factor, no known practical breaks
**Cons:** not memory-hard — a well-funded attacker with GPU/ASIC/FPGA clusters gets meaningfully more advantage against bcrypt than Argon2id per dollar spent; 72-byte input truncation is a known footgun if not handled explicitly

### Option C: PBKDF2
| Dimension | Assessment |
|-----------|------------|
| Complexity | Low — built into .NET BCL (`Rfc2898DeriveBytes`), no third-party dependency |
| Cost | N/A |
| Scalability | Iteration count tunable, but not memory-hard |
| Learning value | Medium — teaches key-stretching fundamentals, and is the only option here with zero external dependencies, but is the weakest of the three against dedicated cracking hardware |

**Pros:** no external package, FIPS-approved (relevant if a real compliance target existed), simple to reason about
**Cons:** not memory-hard at all — of the three, the cheapest for an attacker to accelerate with GPUs, since it's pure CPU-bound iteration with negligible memory footprint

## Trade-off Analysis

The deciding factor is what a memory-hard algorithm specifically defends against: an attacker who's exfiltrated the `Users` table isn't limited to CPU-bound guessing — they'll reach for GPUs or purpose-built ASICs, which parallelize cheap, memory-light hash functions extremely well. SHA-256 alone is wrong here for exactly that reason: it's *designed* to be fast, which is the opposite of what a password hash needs. bcrypt and PBKDF2 both raise the CPU cost of each guess (via work factor / iteration count), but neither forces an attacker to also pay for memory bandwidth per parallel guess — a GPU cluster with thousands of cheap cores can still brute-force them far faster than a general-purpose CPU can verify one login. Argon2id's memory-hardness (64 MB per hash, here) means an attacker's parallelism is bounded by how much memory they can throw at the problem, not just compute — GPUs have abundant cores but comparatively little memory per core, which is precisely the asymmetry Argon2id is designed to exploit against them. Combined with being the current OWASP-recommended default and the PHC (Password Hashing Competition) winner, this outweighs bcrypt's longer production track record and PBKDF2's zero-dependency BCL availability.

Storing tuning parameters alongside the hash (rather than as a single server-wide constant) was a small but deliberate design choice made during implementation (Day 18): it means the memory/iteration/parallelism cost can be raised later — as hardware gets cheaper for attackers — without a forced rehash-everyone-at-once migration. Existing hashes keep verifying against their own embedded parameters; only newly-set passwords pick up the new defaults.

## Consequences

- Each login costs ~64 MB of server-side memory for the duration of the Argon2id computation — bounded and acceptable at this project's scale, but a real capacity-planning input at high concurrent login volume (mitigated somewhat by the Day 22 login rate limiter, which caps how many concurrent Argon2id computations a single attacker can trigger)
- No .NET BCL support means an external dependency (`Konscious.Security.Cryptography.Argon2`) is now part of the trusted computing base for every credential in the system — acceptable, but worth remembering if that package's maintenance status is ever in question
- Tuning can be raised over time (increase iterations/memory in `Argon2idPasswordHasher`'s constants) without invalidating already-issued hashes, since each hash carries its own parameters
- Revisit trigger: if login latency/server memory pressure from concurrent Argon2id computations ever becomes a measurable problem at a scale this project doesn't currently operate at, that's the moment to reconsider parameters (lower memory cost) or add a queueing/backpressure mechanism in front of the hasher — not a case for switching algorithms

## Action Items

1. [x] Implement `Argon2idPasswordHasher` (`src/Services/Identity/Identity.Infrastructure/Argon2idPasswordHasher.cs`)
2. [x] Store tuning parameters alongside the hash so they can change without invalidating existing hashes
3. [x] Pair with login rate limiting (Day 22, `LoginRateLimiting.cs`) to bound the server-side memory cost an unauthenticated attacker can trigger

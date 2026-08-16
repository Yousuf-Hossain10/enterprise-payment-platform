# BuildingBlocks.Security

JWT bearer validation and claims-based permission checks, configured once — per `docs/Security-Model.md` §1, every service validates the same JWT issued by Identity; no service issues or re-authenticates its own tokens.

## `AddPlatformJwtAuthentication()` (Day 16)

Wires JWT bearer authentication (`Microsoft.AspNetCore.Authentication.JwtBearer`) using `JwtOptions` (`Issuer`, `Audience`, `SigningKey` — bound + validated on startup via `BuildingBlocks.Common`'s `AddValidatedOptions`), plus the `[RequirePermission]` policy provider and handler below.

```csharp
builder.Services.AddPlatformJwtAuthentication();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

```json
{ "Jwt": { "Issuer": "...", "Audience": "...", "SigningKey": "..." } }
```

## `[RequirePermission("wallet:debit")]`

A claims-based authorization attribute so permission checks aren't copy-pasted per service as ad hoc role comparisons, per `docs/Coding-Standards.md`:

```csharp
[RequirePermission("wallet:debit")]
public IActionResult Debit(DebitRequest request) { ... }
```

Backed by `PermissionPolicyProvider`, which builds the underlying `AuthorizationPolicy` on demand for any policy name of the form `Permission:{permission}` — permissions don't need to be pre-registered with `AddAuthorization()` one by one. `PermissionAuthorizationHandler` succeeds the requirement if the caller's JWT carries a `permission` claim (`PermissionAuthorizationHandler.ClaimType`) with a matching value; Identity (Phase 5) is what issues those claims.

**Testing note:** verified end-to-end via `TestServer` with real signed JWTs (`System.IdentityModel.Tokens.Jwt`, no live Identity Service) — no token → 401, valid token missing the permission claim → 403, valid token with it → 200, and a token signed with the wrong key → 401. `PermissionAuthorizationHandler` and `PermissionPolicyProvider` are also unit tested in isolation.

## Phase 4 Definition of Done

All four `BuildingBlocks.*` libraries build, have tests, and have READMEs (this is the fourth) — see the throwaway `Ping.Api` under `src/BuildingBlocks/` for the composition proof: a minimal service referencing all four and booting successfully.

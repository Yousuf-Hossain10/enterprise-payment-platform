using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Security;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// The JWT claim type Identity (Phase 5) issues one of per permission granted.
    /// </summary>
    public const string ClaimType = "permission";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(ClaimType, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

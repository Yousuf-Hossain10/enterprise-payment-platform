using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Security.Tests;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(PermissionRequirement requirement, ClaimsPrincipal user) =>
        new([requirement], user, resource: null);

    [Fact]
    public async Task Succeeds_WhenUserHasMatchingPermissionClaim()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("wallet:debit");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionAuthorizationHandler.ClaimType, "wallet:debit")
        ]));
        var context = CreateContext(requirement, user);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task DoesNotSucceed_WhenUserLacksPermissionClaim()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("wallet:debit");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PermissionAuthorizationHandler.ClaimType, "wallet:read")
        ]));
        var context = CreateContext(requirement, user);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task DoesNotSucceed_WhenUserHasNoClaims()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("wallet:debit");
        var context = CreateContext(requirement, new ClaimsPrincipal(new ClaimsIdentity()));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

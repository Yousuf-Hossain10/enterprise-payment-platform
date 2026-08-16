using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Security.Tests;

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider CreateProvider() =>
        new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task BuildsPermissionRequirement_ForPermissionPrefixedPolicy()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("Permission:wallet:debit");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("wallet:debit", requirement.Permission);
    }

    [Fact]
    public async Task FallsBackToDefaultProvider_ForNonPermissionPolicy()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("SomeOtherPolicy");

        Assert.Null(policy);
    }
}

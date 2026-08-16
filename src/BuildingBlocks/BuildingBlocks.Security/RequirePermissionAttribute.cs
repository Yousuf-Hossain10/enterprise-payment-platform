using Microsoft.AspNetCore.Authorization;

namespace BuildingBlocks.Security;

/// <summary>
/// Claims-based permission check, e.g. [RequirePermission("wallet:debit")],
/// per docs/Coding-Standards.md - authorization logic isn't copy-pasted per
/// service as ad hoc role checks. Backed by <see cref="PermissionPolicyProvider"/>,
/// which builds the underlying policy on demand rather than requiring every
/// permission to be pre-registered.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public RequirePermissionAttribute(string permission) : base(PolicyPrefix + permission)
    {
    }
}

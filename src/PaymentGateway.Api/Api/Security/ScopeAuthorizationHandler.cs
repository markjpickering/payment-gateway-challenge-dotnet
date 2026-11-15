using Microsoft.AspNetCore.Authorization;

namespace PaymentGateway.Api.Security;

/// <summary>
/// Succeeds when the token's <c>scope</c> claim lists the required scope. OAuth carries all scopes in one
/// space-separated claim, so <c>RequireClaim</c> (which compares whole values) cannot be used (ADR-7).
/// </summary>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
{
    private const string ScopeClaim = "scope";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var granted = context.User.FindAll(ScopeClaim).SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (granted.Contains(requirement.Scope, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
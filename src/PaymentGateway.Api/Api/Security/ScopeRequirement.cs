using Microsoft.AspNetCore.Authorization;

namespace PaymentGateway.Api.Security;

public sealed record ScopeRequirement(string Scope) : IAuthorizationRequirement;
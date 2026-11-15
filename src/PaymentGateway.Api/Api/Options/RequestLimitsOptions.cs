using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Options;

/// <summary>Limits on what the API will read from a caller (ADR-11), validated at startup.</summary>
public sealed record RequestLimitsOptions
{
    public const string SectionName = "RequestLimits";

    /// <summary>The largest request body accepted. A payment request is a few hundred bytes, so 4 KB is generous.</summary>
    [Range(1, 1_000_000)]
    public long MaxRequestBodyBytes { get; init; } = 4096;
}
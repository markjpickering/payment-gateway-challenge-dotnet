using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.DataLayer.Bank;

/// <summary>Configuration for the acquiring bank client, validated at startup (ADR-12).</summary>
public sealed record BankOptions
{
    public const string SectionName = "Bank";

    [Required]
    public Uri? BaseUrl { get; init; }

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
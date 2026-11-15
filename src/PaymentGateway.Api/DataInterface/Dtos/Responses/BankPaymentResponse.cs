using System.Text.Json.Serialization;

namespace PaymentGateway.DataInterface.Dtos.Responses;

/// <summary>The bank's answer. <c>authorized</c> is required: a body without it is unreadable, not a decline.</summary>
public record BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    [JsonRequired]
    public bool Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string AuthorizationCode { get; init; } = string.Empty;
}
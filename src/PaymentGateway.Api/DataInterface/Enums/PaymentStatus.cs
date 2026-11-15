using System.Text.Json.Serialization;

namespace PaymentGateway.DataInterface.Enums;

/// <summary>Serialised as its name ("Authorized"), as the brief names the statuses, not as a number.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Authorized,
    Declined,
    Rejected
}
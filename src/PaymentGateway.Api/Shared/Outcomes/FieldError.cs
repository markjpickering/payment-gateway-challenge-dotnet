namespace PaymentGateway.Shared.Outcomes;

/// <summary>
/// One problem with one field of a request. The message names the rule and never contains the
/// submitted value, so card data cannot leak (ADR-17).
/// </summary>
public record FieldError(string Field, string Message);
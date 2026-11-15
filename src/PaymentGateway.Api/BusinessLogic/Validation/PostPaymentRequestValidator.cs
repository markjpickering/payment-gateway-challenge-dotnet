using FluentValidation;

namespace PaymentGateway.BusinessLogic.Validation;

/// <summary>
/// Validates a payment request against the brief's rules plus one deliberate addition: the amount must be positive (ADR-8). Messages name the field
/// and the rule but never contain the submitted value, so card data cannot leak (ADR-17).
/// </summary>
public class PostPaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    private static readonly string[] AllowedCurrencies = ["USD", "GBP", "EUR"];

    private const string DigitsOnly = @"\A[0-9]+\z";

    private readonly TimeProvider _timeProvider;

    public PostPaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        ConfigureCardNumber();
        ConfigureExpiryMonth();
        ConfigureExpiryYear();
        ConfigureCurrency();
        ConfigureAmount();
        ConfigureCvv();
    }

    private void ConfigureCardNumber() =>
        RuleFor(r => r.CardNumber).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Card number is required.")
            .Length(14, 19).WithMessage("Card number must be 14 to 19 characters long.")
            .Matches(DigitsOnly).WithMessage("Card number must contain only digits.");

    private void ConfigureExpiryMonth() =>
        RuleFor(r => r.ExpiryMonth).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Expiry month is required.")
            .InclusiveBetween(1, 12).WithMessage("Expiry month must be between 1 and 12.");

    private void ConfigureExpiryYear() =>
        RuleFor(r => r.ExpiryYear).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Expiry year is required.")
            .InclusiveBetween(1, 9999).WithMessage("Expiry year must be a four-digit year.")
            .Must((request, year) => ExpiryIsNotInThePast(request.ExpiryMonth!.Value, year!.Value))
            .When(request => request.ExpiryMonth is >= 1 and <= 12, ApplyConditionTo.CurrentValidator)
            .WithMessage("The expiry month and year must be in the future.");

    private void ConfigureCurrency() =>
        RuleFor(r => r.Currency)
            .Must(currency => AllowedCurrencies.Contains(currency))
            .WithMessage($"Currency must be one of {string.Join(", ", AllowedCurrencies)}.");

    private void ConfigureAmount() =>
        RuleFor(r => r.Amount).Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Amount is required.")
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

    private void ConfigureCvv() =>
        RuleFor(r => r.Cvv).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("CVV is required.")
            .Length(3, 4).WithMessage("CVV must be 3 or 4 characters long.")
            .Matches(DigitsOnly).WithMessage("CVV must contain only digits.");

    // A card is valid through the end of its expiry month, so the current month is still acceptable.
    // Comparing month indexes avoids date arithmetic (and overflow for year 9999).
    private bool ExpiryIsNotInThePast(int month, int year)
    {
        var now = _timeProvider.GetUtcNow();

        return year * 12 + month >= now.Year * 12 + now.Month;
    }
}
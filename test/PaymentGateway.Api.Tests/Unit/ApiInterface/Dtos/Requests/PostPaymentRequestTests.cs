namespace PaymentGateway.Api.Tests.Unit.ApiInterface.Dtos.Requests;

public class PostPaymentRequestTests
{
    private const string CardNumber = "2222405343248877";
    private const string Cvv = "123";

    // Shared Arrange for every test: a fully populated request.
    private readonly PostPaymentRequest _sut = new()
    {
        CardNumber = CardNumber,
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100,
        Cvv = Cvv
    };

    [Fact]
    public void ToString_DoesNotRevealTheCardNumberOrCvv()
    {
        // Arrange: the request (_sut) is created in the field initialiser

        // Act
        var text = _sut.ToString();

        // Assert
        text.Should().NotContain(CardNumber).And.NotContain("8877").And.NotContain(Cvv);
    }

    [Fact]
    public void StringInterpolation_DoesNotRevealTheCardNumberOrCvv()
    {
        // Arrange: the request (_sut) is created in the field initialiser

        // Act
        var text = $"{_sut}";

        // Assert
        text.Should().NotContain(CardNumber).And.NotContain(Cvv);
    }

    [Fact]
    public void ToString_StillShowsNonSensitiveFields()
    {
        // Arrange: the request (_sut) is created in the field initialiser

        // Act
        var text = _sut.ToString();

        // Assert
        text.Should().Contain("GBP").And.Contain("100");
    }
}
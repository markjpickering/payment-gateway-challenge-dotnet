namespace PaymentGateway.Api.Tests.Unit.DataInterface.Dtos.Requests;

public class BankPaymentRequestTests
{
    [Fact]
    public void ToString_DoesNotRevealTheCardNumberOrCvv()
    {
        // Arrange
        var sut = BankClientTestData.ValidBankRequest();

        // Act
        var text = sut.ToString();

        // Assert
        text.Should().NotContain(BankClientTestData.CardNumber).And.NotContain("123");
    }

    [Fact]
    public void ToString_StillShowsNonSensitiveFields()
    {
        // Arrange
        var sut = BankClientTestData.ValidBankRequest();

        // Act
        var text = sut.ToString();

        // Assert
        text.Should().Contain("04/2030").And.Contain("GBP");
    }
}
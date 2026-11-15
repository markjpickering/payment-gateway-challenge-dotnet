using System.Net;
using System.Text.Json.Nodes;

namespace PaymentGateway.Api.Tests.Unit.DataLayer.Bank.BankClientTests;

public class SuccessfulResponseTestClass
{
    [Fact]
    public async Task AuthorizeAsync_BankAuthorizes_ReturnsTheAuthorizedResponse()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Responding(
            HttpStatusCode.OK, """{"authorized": true, "authorization_code": "0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""");
        var sut = BankClientTestData.NewClient(handler);

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Authorized.Should().BeTrue();
        result.Value.AuthorizationCode.Should().Be("0bb07405-6d44-4b50-a14f-7ae0beff13ad");
    }

    [Fact]
    public async Task AuthorizeAsync_BankDeclines_ReturnsTheDeclinedResponse()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Responding(
            HttpStatusCode.OK, """{"authorized": false, "authorization_code": ""}""");
        var sut = BankClientTestData.NewClient(handler);

        // Act
        var result = await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_AnyRequest_PostsSnakeCaseJsonToThePaymentsRoute()
    {
        // Arrange
        var handler = StubHttpMessageHandler.Responding(HttpStatusCode.OK, """{"authorized": true, "authorization_code": "x"}""");
        var sut = BankClientTestData.NewClient(handler);

        // Act
        await sut.AuthorizeAsync(BankClientTestData.ValidBankRequest(), TestContext.Current.CancellationToken);

        // Assert
        var body = JsonNode.Parse(handler.LastBody!)!;
        handler.LastMethod.Should().Be(HttpMethod.Post);
        handler.LastUri!.AbsolutePath.Should().Be("/payments");
        body["card_number"]!.GetValue<string>().Should().Be(BankClientTestData.CardNumber);
        body["expiry_date"]!.GetValue<string>().Should().Be("04/2030");
        body["currency"]!.GetValue<string>().Should().Be("GBP");
        body["amount"]!.GetValue<int>().Should().Be(100);
        body["cvv"]!.GetValue<string>().Should().Be("123");
    }
}
using System.Net.Sockets;

using Microsoft.Extensions.Logging.Abstractions;

namespace PaymentGateway.Api.Tests.Integration.Simulator;

/// <summary>
/// The real <see cref="BankClient"/> against the real Mountebank bank simulator (ADR-6). They need
/// <c>docker-compose up -d</c> and are skipped when the simulator is not reachable, so a plain <c>dotnet test</c> stays green.
/// Run only these with <c>--filter-trait "Category=Simulator"</c>.
/// </summary>
[Trait("Category", "Simulator")]
public class SimulatorBankTests
{
    private static readonly Uri SimulatorUrl = new("http://localhost:8080");

    private static bool SimulatorIsRunning()
    {
        try
        {
            using var tcp = new TcpClient();

            return tcp.ConnectAsync(SimulatorUrl.Host, SimulatorUrl.Port).Wait(TimeSpan.FromMilliseconds(500));
        }
        catch (Exception exception) when (exception is SocketException or AggregateException)
        {
            return false;
        }
    }

    private static BankClient NewClient()
    {
        Assert.SkipUnless(SimulatorIsRunning(), "The bank simulator is not running (docker-compose up -d).");

        return new BankClient(
            new HttpClient { BaseAddress = SimulatorUrl, Timeout = TimeSpan.FromSeconds(5) },
            NullLogger<BankClient>.Instance, Substitute.For<IPaymentMetrics>(), TimeProvider.System);
    }

    private static BankPaymentRequest RequestFor(string cardNumber) => new()
    {
        CardNumber = cardNumber,
        ExpiryDate = "04/2030",
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public async Task AuthorizeAsync_CardEndingInAnOddDigit_IsAuthorizedWithACode()
    {
        // Arrange
        var sut = NewClient();

        // Act
        var result = await sut.AuthorizeAsync(RequestFor("2222405343248877"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Authorized.Should().BeTrue();
        result.Value.AuthorizationCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthorizeAsync_CardEndingInAnEvenDigit_IsDeclined()
    {
        // Arrange
        var sut = NewClient();

        // Act
        var result = await sut.AuthorizeAsync(RequestFor("2222405343248112"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Authorized.Should().BeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_CardEndingInZero_IsABankErrorBecauseTheBankIsUnavailable()
    {
        // Arrange
        var sut = NewClient();

        // Act
        var result = await sut.AuthorizeAsync(RequestFor("2222405343248870"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.BankError);
    }
}
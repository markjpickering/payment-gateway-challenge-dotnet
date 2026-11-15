using System.Reflection;
using System.Text.RegularExpressions;

namespace PaymentGateway.Api.Tests.Unit.PaymentGatewayDiTests;

/// <summary>
/// The DI class has exactly one public method, <c>InitialiseDi</c>, and it is the first method in the file, so a reader
/// opening it sees at once where the wiring starts. The per-concern registrations below it are internal helpers.
/// </summary>
public class EntryPointTestClass
{
    private static string SourceOfTheDiClass()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PaymentGateway.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(directory!.FullName, "src", "PaymentGateway.Api", "PaymentGatewayDi.cs"));
    }

    [Fact]
    public void ThePaymentGatewayDiClass_HasExactlyOnePublicMethod_InitialiseDi()
    {
        // Arrange
        var type = typeof(PaymentGatewayDi);

        // Act
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name);

        // Assert
        publicMethods.Should().Equal("InitialiseDi");
    }

    [Fact]
    public void InitialiseDi_IsTheFirstMethodInTheFile()
    {
        // Arrange
        var source = SourceOfTheDiClass();

        // Act
        var firstMethod = Regex.Match(source, @"^\s*(?:public|internal) static \S+ (\w+)\(", RegexOptions.Multiline).Groups[1].Value;

        // Assert
        firstMethod.Should().Be("InitialiseDi");
    }
}
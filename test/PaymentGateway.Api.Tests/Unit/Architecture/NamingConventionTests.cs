using NetArchTest.Rules;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// A type's name tells you its role (ADR-27): DTOs end in <c>Request</c> or <c>Response</c>, entities in <c>Entity</c>,
/// in-flight business models in <c>Model</c>. There is no <c>Dto</c> suffix, because <c>Request</c>/<c>Response</c> already marks a DTO.
/// </summary>
public class NamingConventionTests
{
    private const string Root = "PaymentGateway";
    private static readonly System.Reflection.Assembly Assembly = typeof(Program).Assembly;

    private static IReadOnlyList<string> NamesNotEndingWith(string namespacePrefix, params string[] suffixes) =>
        [.. Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(namespacePrefix).And().AreClasses().GetTypes()
            .Where(t => !suffixes.Any(s => t.Name.EndsWith(s, StringComparison.Ordinal)))
            .Select(t => t.FullName!)];

    [Fact]
    public void ApiDtos_AreAllRequestsOrResponses()
    {
        // Arrange: the DTOs the endpoints rely on

        // Act
        var offenders = NamesNotEndingWith("PaymentGateway.ApiInterface.Dtos", "Request", "Response");

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void BankDtos_AreAllRequestsOrResponses()
    {
        // Arrange: the bank's DTOs

        // Act
        var offenders = NamesNotEndingWith("PaymentGateway.DataInterface.Dtos", "Request", "Response");

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void BusinessLogicModels_AreAllNamedModel()
    {
        // Arrange: in-flight types that are neither DTOs nor entities

        // Act
        var offenders = NamesNotEndingWith(Root + ".BusinessLogic.Models", "Model");

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void EveryRequestAndResponse_IsADto_SoItLivesInADtosFolder()
    {
        // Arrange: Request/Response is the DTO marker (there is no Dto suffix), so these types belong in Dtos/Requests and Dtos/Responses
        var types = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(Root).And().AreClasses().GetTypes()
            .Where(t => !t.IsNested && !t.Name.Contains('<'))
            .ToList();

        // Act
        var misplacedRequests = types.Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal) && !(t.Namespace ?? "").EndsWith(".Dtos.Requests", StringComparison.Ordinal));
        var misplacedResponses = types.Where(t => t.Name.EndsWith("Response", StringComparison.Ordinal) && !(t.Namespace ?? "").EndsWith(".Dtos.Responses", StringComparison.Ordinal));

        // Assert
        misplacedRequests.Select(t => t.FullName).Concat(misplacedResponses.Select(t => t.FullName)).Should().BeEmpty();
    }

    [Fact]
    public void EveryTypeInADtosRequestsOrResponsesFolder_IsNamedRequestOrResponse()
    {
        // Arrange: nothing else (no helper, no entity) hides in a Dtos folder
        var types = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(Root).And().AreClasses().GetTypes()
            .Where(t => !t.IsNested && !t.Name.Contains('<'));

        // Act
        var offenders = types
            .Where(t => (t.Namespace ?? "").EndsWith(".Dtos.Requests", StringComparison.Ordinal) && !t.Name.EndsWith("Request", StringComparison.Ordinal)
                || (t.Namespace ?? "").EndsWith(".Dtos.Responses", StringComparison.Ordinal) && !t.Name.EndsWith("Response", StringComparison.Ordinal))
            .Select(t => t.FullName);

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void NoTypeUsesTheDtoSuffix_BecauseRequestAndResponseAlreadyMarkADto()
    {
        // Arrange: every type of ours

        // Act
        var offenders = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(Root)
            .And().HaveNameEndingWith("Dto").GetTypes().Select(t => t.FullName!);

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void TestClasses_NeverUseThePluralTestClassesSuffix()
    {
        // Arrange: each functional-area file holds a single test class, so it is a TestClass, not TestClasses
        var testAssembly = typeof(NamingConventionTests).Assembly;

        // Act
        var offenders = testAssembly.GetTypes().Where(t => !t.IsNested && t.Name.EndsWith("TestClasses", StringComparison.Ordinal)).Select(t => t.FullName!);

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void TestClassesInASplitFolder_AreAllNamedTestClass()
    {
        // Arrange: a folder named [ClassUnderTest]Tests/ holds one [FunctionalArea]TestClass per area
        var testAssembly = typeof(NamingConventionTests).Assembly;

        // Act
        var offenders = testAssembly.GetTypes()
            .Where(t => !t.IsNested && !t.Name.Contains('<') && t.Namespace is { } ns
                && ns.StartsWith("PaymentGateway.Api.Tests.Unit.", StringComparison.Ordinal) && ns.Split('.')[^1].EndsWith("Tests", StringComparison.Ordinal))
            .Where(t => !t.Name.EndsWith("TestClass", StringComparison.Ordinal))
            .Select(t => t.FullName!);

        // Assert
        offenders.Should().BeEmpty();
    }
}
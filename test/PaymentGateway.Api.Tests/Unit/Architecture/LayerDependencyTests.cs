using NetArchTest.Rules;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// Enforces the layering (ADR-27) so it cannot erode. The API's contract (<c>ApiInterface</c>: the request and response
/// DTOs) and the data contract (<c>DataInterface</c>: stores, bank, entities) sit either side of the business logic:
/// Api (endpoints) -> BusinessLogic -> ApiInterface and DataInterface, with DataLayer implementing DataInterface and
/// wired in only by the composition code, and Shared (<c>Result</c>, <c>Error</c>) below everything and depending on
/// nothing. The project's root namespace is <c>PaymentGateway</c>, so each layer is a namespace such as
/// <c>PaymentGateway.DataLayer</c>. NetArchTest matches namespaces by string prefix, so the Api layer is written with a
/// trailing dot: <c>PaymentGateway.Api.</c> must not also match <c>PaymentGateway.ApiInterface</c>.
/// </summary>
public class LayerDependencyTests
{
    private const string Api = "PaymentGateway.Api.";
    private const string ApiInterface = "PaymentGateway.ApiInterface";
    private const string BusinessLogic = "PaymentGateway.BusinessLogic";
    private const string DataInterface = "PaymentGateway.DataInterface";
    private const string DataLayer = "PaymentGateway.DataLayer";
    private const string Shared = "PaymentGateway.Shared";

    private static readonly System.Reflection.Assembly Assembly = typeof(Program).Assembly;

    private static NetArchTest.Rules.TestResult CheckThat(string namespacePrefix, params string[] mustNotDependOn) =>
        Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(namespacePrefix)
            .ShouldNot().HaveDependencyOnAny(mustNotDependOn).GetResult();

    private static string Offenders(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);

    [Fact]
    public void Shared_HoldsTheOutcomeTypes_AndDependsOnNoLayer()
    {
        // Arrange
        var types = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(Shared).GetTypes();

        // Act
        var result = CheckThat(Shared, Api, ApiInterface, BusinessLogic, DataInterface, DataLayer);

        // Assert
        types.Should().NotBeEmpty("Result, Error and ErrorKind live in Shared");
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }

    [Fact]
    public void DataInterface_DependsOnNothingAbove_ItIsTheLowestLayerOfOurs()
    {
        // Arrange: it may use only Shared

        // Act
        var result = CheckThat(DataInterface, DataLayer, Api, ApiInterface, BusinessLogic);

        // Assert
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }

    [Fact]
    public void ApiInterface_IsJustTheContract_UsingOnlyDataInterfaceEnumsAndShared()
    {
        // Arrange: the request and response DTOs share the status enums with the entities, nothing more

        // Act
        var result = CheckThat(ApiInterface, DataLayer, Api, BusinessLogic);

        // Assert
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }

    [Fact]
    public void DataLayer_ImplementsDataInterfaceOnly_NeverTheBusinessLogicOrTheApi()
    {
        // Arrange: it may use DataInterface and Shared

        // Act
        var result = CheckThat(DataLayer, Api, ApiInterface, BusinessLogic);

        // Assert
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }

    [Fact]
    public void BusinessLogic_NeverReachesTheDataLayerOrTheEndpoints()
    {
        // Arrange: it may use ApiInterface (the DTOs it accepts and returns), DataInterface and Shared

        // Act
        var result = CheckThat(BusinessLogic, DataLayer, Api);

        // Assert
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }

    [Fact]
    public void Api_NeverReachesTheDataLayerDirectly()
    {
        // Arrange: it talks to the business logic and uses the ApiInterface DTOs

        // Act
        var result = CheckThat(Api, DataLayer);

        // Assert
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }

    [Fact]
    public void ApiDtos_LiveInTheApiInterface_NotInTheBusinessLogicOrTheApi()
    {
        // Arrange: the request and response DTOs are the API's interface
        var dtos = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(ApiInterface + ".Dtos").GetTypes();
        var leftInBusinessLogic = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(BusinessLogic + ".Interfaces.Dtos").GetTypes();
        var leftInApi = Types.InAssembly(Assembly).That().ResideInNamespaceStartingWith(Api + "Models").GetTypes();

        // Act
        var count = dtos.Count();

        // Assert
        count.Should().BeGreaterThan(0, "the DTOs are in ApiInterface/Dtos");
        leftInBusinessLogic.Should().BeEmpty("the DTOs are not part of BusinessLogic/Interfaces");
        leftInApi.Should().BeEmpty("there is no Api/Models");
    }

    [Fact]
    public void EntitiesAreNamedAsSuch()
    {
        // Arrange: every entity type ends in "Entity" (ADR-26)

        // Act
        var result = Types.InAssembly(Assembly).That().ResideInNamespace(DataInterface + ".Entities")
            .And().AreClasses().Should().HaveNameEndingWith("Entity").GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(because: Offenders(result));
    }
}
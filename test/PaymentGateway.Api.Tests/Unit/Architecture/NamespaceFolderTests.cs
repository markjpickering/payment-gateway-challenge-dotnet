using System.Text.RegularExpressions;

namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// Namespaces follow the folders (ADR-27): a file's namespace is the project's root namespace plus its folder path, so a
/// reader can find a type from its namespace and a namespace from its place in the tree. The API project's root
/// namespace is <c>PaymentGateway</c>; the test project's is <c>PaymentGateway.Api.Tests</c>.
/// </summary>
public partial class NamespaceFolderTests
{
    // Program.cs holds top-level statements (no namespace) and Usings.cs only holds global usings.
    private static readonly string[] FilesWithoutANamespace = ["Program.cs", "Usings.cs"];

    [GeneratedRegex(@"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PaymentGateway.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (PaymentGateway.sln) was not found.");
    }

    private static IEnumerable<string> Mismatches(string projectFolder, string rootNamespace)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        foreach (var file in Directory.EnumerateFiles(projectFolder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectFolder, file);
            var segments = relative.Split(separators);
            if (segments.Any(s => s is "bin" or "obj") || FilesWithoutANamespace.Contains(segments[^1]))
            {
                continue;
            }

            var expected = string.Join('.', new[] { rootNamespace }.Concat(segments[..^1]));
            var declared = NamespaceDeclaration().Match(File.ReadAllText(file)).Groups[1].Value;
            if (declared != expected)
            {
                yield return $"{relative.Replace('\\', '/')}: declared '{declared}', expected '{expected}'";
            }
        }
    }

    [Fact]
    public void EveryFileInTheApiProject_DeclaresTheNamespaceOfItsFolder()
    {
        // Arrange
        var project = Path.Combine(RepositoryRoot(), "src", "PaymentGateway.Api");

        // Act
        var mismatches = Mismatches(project, "PaymentGateway");

        // Assert
        mismatches.Should().BeEmpty();
    }

    [Fact]
    public void EveryFileInTheTestProject_DeclaresTheNamespaceOfItsFolder()
    {
        // Arrange
        var project = Path.Combine(RepositoryRoot(), "test", "PaymentGateway.Api.Tests");

        // Act
        var mismatches = Mismatches(project, "PaymentGateway.Api.Tests");

        // Assert
        mismatches.Should().BeEmpty();
    }

    [Fact]
    public void TheApiProjectsRootNamespace_IsPaymentGateway()
    {
        // Arrange: the layers are namespaces of their own (PaymentGateway.Api, .BusinessLogic, ...), never Api.Api
        var project = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PaymentGateway.Api", "PaymentGateway.Api.csproj"));

        // Act
        var hasRootNamespace = project.Contains("<RootNamespace>PaymentGateway</RootNamespace>", StringComparison.Ordinal);

        // Assert
        hasRootNamespace.Should().BeTrue();
    }
}
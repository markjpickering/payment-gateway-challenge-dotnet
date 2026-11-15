namespace PaymentGateway.Api.Tests.Unit.Architecture;

/// <summary>
/// A reviewer browsing the repository on GitHub sees a folder's README under its file list (ADR-28), so every top-level
/// folder of the API project, and the test project, explains itself: what it is, what it may depend on, where to start.
/// </summary>
public class FolderReadmeTests
{
    private static readonly string[] NotCodeFolders = ["bin", "obj", "Properties"];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PaymentGateway.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (PaymentGateway.sln) was not found.");
    }

    [Fact]
    public void EveryTopLevelFolderOfTheApiProject_HasAReadme()
    {
        // Arrange
        var project = Path.Combine(RepositoryRoot(), "src", "PaymentGateway.Api");

        // Act
        var missing = Directory.GetDirectories(project)
            .Where(d => !NotCodeFolders.Contains(Path.GetFileName(d)))
            .Where(d => !File.Exists(Path.Combine(d, "README.md")))
            .Select(Path.GetFileName);

        // Assert
        missing.Should().BeEmpty("each layer folder needs a README.md saying what it is and what it may depend on");
    }

    [Fact]
    public void TheTestProject_HasAReadme()
    {
        // Arrange
        var readme = Path.Combine(RepositoryRoot(), "test", "PaymentGateway.Api.Tests", "README.md");

        // Act
        var exists = File.Exists(readme);

        // Assert
        exists.Should().BeTrue("the test project explains its layout");
    }

    [Fact]
    public void EveryFolderReadme_StartsWithAHeadingAndSaysWhatItMayDependOn()
    {
        // Arrange
        var project = Path.Combine(RepositoryRoot(), "src", "PaymentGateway.Api");
        var layers = new[] { "Api", "ApiInterface", "BusinessLogic", "DataInterface", "DataLayer", "Shared" };

        // Act
        var weak = layers
            .Select(layer => (Layer: layer, Text: File.ReadAllText(Path.Combine(project, layer, "README.md"))))
            .Where(r => !r.Text.StartsWith("# ", StringComparison.Ordinal) || !(r.Text.Contains("May depend on", StringComparison.Ordinal) || r.Text.Contains("Depends on", StringComparison.Ordinal)))
            .Select(r => r.Layer);

        // Assert
        weak.Should().BeEmpty("a layer README states its dependency rule");
    }
}
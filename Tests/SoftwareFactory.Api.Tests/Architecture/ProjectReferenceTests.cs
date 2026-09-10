using System.Xml.Linq;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Dependency rule at the project level, read from the csproj files (estandar-backend.md §1):
/// Domain references nobody, Application only Domain, Infrastructure and the hosts only Application (and Domain);
/// the hosts also reference Infrastructure because composition (DI) lives in them.
/// </summary>
public sealed class ProjectReferenceTests
{
    [Fact]
    public void Domain_project_references_no_other_project() =>
        Assert.Empty(ProjectReferencesOf(SolutionLayout.Domain));

    [Fact]
    public void Application_project_references_only_domain() =>
        Assert.Equal([SolutionLayout.Domain], ProjectReferencesOf(SolutionLayout.Application));

    [Theory]
    [InlineData(SolutionLayout.Infrastructure, SolutionLayout.Domain, SolutionLayout.Application)]
    [InlineData(SolutionLayout.Api, SolutionLayout.Domain, SolutionLayout.Application, SolutionLayout.Infrastructure)]
    [InlineData(SolutionLayout.AgentRuntime, SolutionLayout.Domain, SolutionLayout.Application, SolutionLayout.Infrastructure)]
    public void Project_references_only_allowed_projects(string project, params string[] allowed)
    {
        var references = ProjectReferencesOf(project);

        var forbidden = references.Except(allowed, StringComparer.Ordinal).ToList();

        Assert.True(forbidden.Count == 0, $"{project} must not reference: {string.Join(", ", forbidden)}");
    }

    [Theory]
    [InlineData(SolutionLayout.Api)]
    [InlineData(SolutionLayout.AgentRuntime)]
    public void Host_project_references_application_layer(string host) =>
        Assert.Contains(SolutionLayout.Application, ProjectReferencesOf(host));

    private static List<string> ProjectReferencesOf(string projectName)
    {
        var root = SolutionLayout.FindRepositoryRoot();

        var projectFile = Directory
            .EnumerateFiles(root, $"{projectName}.csproj", SearchOption.AllDirectories)
            .Single();

        return
        [
            .. XDocument.Load(projectFile)
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
                .Order(StringComparer.Ordinal),
        ];
    }
}

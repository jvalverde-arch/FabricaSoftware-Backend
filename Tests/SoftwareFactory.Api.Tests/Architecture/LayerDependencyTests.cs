using NetArchTest.Rules;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Dependency rule at the type level (estandar-backend.md §1), checked over the compiled assemblies with NetArchTest.
/// </summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_does_not_depend_on_outer_layers()
    {
        AssertNoDependency(
            SolutionLayout.Domain,
            SolutionLayout.Application,
            SolutionLayout.Infrastructure,
            SolutionLayout.Api,
            SolutionLayout.AgentRuntime);
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        AssertNoDependency(
            SolutionLayout.Application,
            SolutionLayout.Infrastructure,
            SolutionLayout.Api,
            SolutionLayout.AgentRuntime);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_presentation()
    {
        AssertNoDependency(
            SolutionLayout.Infrastructure,
            SolutionLayout.Api,
            SolutionLayout.AgentRuntime);
    }

    [Fact]
    public void Api_does_not_depend_on_agent_runtime() =>
        AssertNoDependency(SolutionLayout.Api, SolutionLayout.AgentRuntime);

    [Fact]
    public void Agent_runtime_does_not_depend_on_api() =>
        AssertNoDependency(SolutionLayout.AgentRuntime, SolutionLayout.Api);

    private static void AssertNoDependency(string assemblyName, params string[] forbiddenNamespaces)
    {
        var result = Types
            .InAssembly(SolutionLayout.LoadAssembly(assemblyName))
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assemblyName} depends on a forbidden layer. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}

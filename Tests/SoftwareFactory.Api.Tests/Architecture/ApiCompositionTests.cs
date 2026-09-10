using NetArchTest.Rules;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Inside the Api host, Infrastructure is visible only where the object graph is composed:
/// <c>Program</c> (global namespace) and <c>SoftwareFactory.Api.Composition</c>. Controllers stay thin and only see Application.
/// </summary>
public sealed class ApiCompositionTests
{
    [Fact]
    public void Only_program_and_composition_reference_infrastructure()
    {
        var result = Types
            .InAssembly(SolutionLayout.LoadAssembly(SolutionLayout.Api))
            .That()
            .ResideInNamespaceStartingWith(SolutionLayout.Api)
            .And()
            .DoNotResideInNamespace(SolutionLayout.ApiComposition)
            .ShouldNot()
            .HaveDependencyOn(SolutionLayout.Infrastructure)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Only Program and {SolutionLayout.ApiComposition} may reference Infrastructure. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Controllers_depend_only_on_application()
    {
        var result = Types
            .InAssembly(SolutionLayout.LoadAssembly(SolutionLayout.Api))
            .That()
            .ResideInNamespace(SolutionLayout.ApiControllers)
            .ShouldNot()
            .HaveDependencyOnAny(SolutionLayout.Infrastructure, SolutionLayout.Domain)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Controllers may only depend on Application. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}

using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Functional modules (Traceability, Brain, Project, ...) are namespaces inside Domain and Application
/// and talk to each other only through their public contracts (sprint-00, T-001).
/// </summary>
public sealed class ModuleIsolationTests
{
    public static TheoryData<string> Modules => [.. SolutionLayout.Modules];

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_module_does_not_reference_other_domain_modules(string module)
    {
        var rule = ModuleIsolationRule.ForDomainModule(module);

        AssertModuleMeetsRule(SolutionLayout.Domain, module, rule);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_module_reaches_other_modules_only_through_contracts(string module)
    {
        var rule = ModuleIsolationRule.ForApplicationModule(module);

        AssertModuleMeetsRule(SolutionLayout.Application, module, rule);
    }

    private static void AssertModuleMeetsRule(string assemblyName, string module, ModuleIsolationRule rule)
    {
        var moduleNamespace = $"{assemblyName}.{module}";

        var result = Types
            .InAssembly(SolutionLayout.LoadAssembly(assemblyName))
            .That()
            .ResideInNamespaceMatching($"^{Regex.Escape(moduleNamespace)}(\\.|$)")
            .Should()
            .MeetCustomRule(rule)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Module {moduleNamespace} crosses module boundaries outside contracts:{Environment.NewLine}{string.Join(Environment.NewLine, rule.Violations)}");
    }
}

using System.Reflection;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Names that describe the solution layout (estandar-backend.md §1). The architecture tests are written against these.
/// </summary>
internal static class SolutionLayout
{
    public const string SolutionFileName = "SoftwareFactory.sln";

    public const string Domain = "SoftwareFactory.Domain";
    public const string Application = "SoftwareFactory.Application";
    public const string Infrastructure = "SoftwareFactory.Infrastructure";
    public const string Api = "SoftwareFactory.Api";
    public const string AgentRuntime = "SoftwareFactory.AgentRuntime";

    /// <summary>Sub-namespace of an Application module that holds its public contracts: the only door between modules.</summary>
    public const string ContractsSegment = "Contracts";

    /// <summary>Functional modules that live as namespaces inside Domain and Application (sprint-00, T-001).</summary>
    public static IReadOnlyList<string> Modules { get; } =
    [
        "Traceability",
        "Brain",
        "Project",
        "FunctionalDesign",
        "Architecture",
        "Testing",
        "Construction",
        "Finops",
    ];

    public static Assembly LoadAssembly(string assemblyName) => Assembly.Load(new AssemblyName(assemblyName));

    /// <summary>Walks up from the test output directory until the solution file is found.</summary>
    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"{SolutionFileName} was not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>True when <paramref name="ns"/> is <paramref name="prefix"/> itself or a sub-namespace of it (dot-bounded).</summary>
    public static bool IsWithin(string ns, string prefix) =>
        string.Equals(ns, prefix, StringComparison.Ordinal)
        || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
}

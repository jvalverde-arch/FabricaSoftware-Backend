using Microsoft.Extensions.Configuration;
using SoftwareFactory.Hosting;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Configuration precedence of both hosts (estandar-backend.md §7): a local file never beats the deployment. The
/// source under test is linked into the two hosts, so one copy of the rule is one test.
/// </summary>
public sealed class HostConfigurationTests
{
    private const string Key = "HostConfigurationProbe__Value";

    [Fact]
    public void The_environment_wins_over_the_local_file()
    {
        var directory = Directory.CreateTempSubdirectory("host-configuration");
        File.WriteAllText(
            Path.Combine(directory.FullName, HostConfiguration.LocalSettingsFile),
            """{"HostConfigurationProbe":{"Value":"from the file","Untouched":"from the file"}}""");
        Environment.SetEnvironmentVariable(Key, "from the environment");

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory.FullName)
                .AddEnvironmentVariables()
                .AddLocalSettings()
                .Build();

            // Added last, read first: what the orchestrator injects wins even though the file arrived afterwards.
            Assert.Equal("from the environment", configuration["HostConfigurationProbe:Value"]);
            // And the file still answers for everything the deployment does not set.
            Assert.Equal("from the file", configuration["HostConfigurationProbe:Untouched"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Key, null);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_host_without_environment_variables_still_reads_the_local_file()
    {
        var directory = Directory.CreateTempSubdirectory("host-configuration");
        File.WriteAllText(
            Path.Combine(directory.FullName, HostConfiguration.LocalSettingsFile),
            """{"HostConfigurationProbe":{"Value":"from the file"}}""");

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory.FullName)
                .AddLocalSettings()
                .Build();

            Assert.Equal("from the file", configuration["HostConfigurationProbe:Value"]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

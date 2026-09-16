using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace SoftwareFactory.Hosting;

/// <summary>
/// Configuration sources shared by both hosts. Compiled into each one (linked source, not a library) because this is
/// hosting policy, not infrastructure.
/// </summary>
internal static class HostConfiguration
{
    public const string LocalSettingsFile = "appsettings.Local.json";

    /// <summary>
    /// Adds the git-ignored local settings <b>before</b> the environment variables (estandar-backend.md §7): what the
    /// orchestrator — or the self-hosted customer — injects must always win over a file that travels in the image.
    /// Appending it, which is the obvious way to write this, silently inverted that: an <c>appsettings.Local.json</c>
    /// baked into an image overrode the container's own configuration, and it took a container to notice.
    /// </summary>
    public static IConfigurationBuilder AddLocalSettings(this IConfigurationBuilder configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var local = new JsonConfigurationSource
        {
            Path = LocalSettingsFile,
            Optional = true,
            ReloadOnChange = true,
        };

        local.ResolveFileProvider();
        configuration.Sources.Insert(FirstEnvironmentVariableSource(configuration), local);

        return configuration;
    }

    /// <summary>Position of the environment variables; the end of the list when the host declares none.</summary>
    private static int FirstEnvironmentVariableSource(IConfigurationBuilder configuration)
    {
        for (var index = 0; index < configuration.Sources.Count; index++)
        {
            if (configuration.Sources[index] is EnvironmentVariablesConfigurationSource)
            {
                return index;
            }
        }

        return configuration.Sources.Count;
    }
}

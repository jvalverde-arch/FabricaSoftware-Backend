using Microsoft.EntityFrameworkCore.Design;

namespace SoftwareFactory.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> run against this class library. Adding a migration never connects to a database;
/// set SOFTWAREFACTORY_DESIGN_CONNECTION to point commands that do connect (database update, script) at a real server.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SoftwareFactoryDbContext>
{
    private const string DefaultConnection = "Host=localhost;Port=5432;Database=softwarefactory;Username=postgres";

    public SoftwareFactoryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SOFTWAREFACTORY_DESIGN_CONNECTION") ?? DefaultConnection;
        return new SoftwareFactoryDbContext(SoftwareFactoryDbContextOptions.Create(connectionString));
    }
}

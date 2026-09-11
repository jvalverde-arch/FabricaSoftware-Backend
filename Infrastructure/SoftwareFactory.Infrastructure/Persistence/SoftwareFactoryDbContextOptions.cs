using Microsoft.EntityFrameworkCore;

namespace SoftwareFactory.Infrastructure.Persistence;

/// <summary>Provider configuration shared by runtime, design time, the initializer and the tests, so all of them build the same model.</summary>
public static class SoftwareFactoryDbContextOptions
{
    public const string MigrationsHistoryTable = "ef_migrations_history";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseVector();
            npgsql.MigrationsHistoryTable(MigrationsHistoryTable);
        });
    }

    public static DbContextOptions<SoftwareFactoryDbContext> Create(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<SoftwareFactoryDbContext>();
        Configure(builder, connectionString);
        return builder.Options;
    }
}

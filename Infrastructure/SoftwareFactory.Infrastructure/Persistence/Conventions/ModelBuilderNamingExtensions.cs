using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>
/// Applies the database naming convention of estandar-backend.md §4: snake_case singular tables, snake_case columns,
/// and predictable constraint names (pk_, fk_, ix_, ux_). Names set explicitly in a configuration are kept.
/// </summary>
internal static class ModelBuilderNamingExtensions
{
    public static void ApplySnakeCaseNaming(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.FindAnnotation(RelationalAnnotationNames.TableName) is null)
            {
                entityType.SetTableName(SnakeCase.Convert(entityType.ClrType.Name));
            }

            var table = entityType.GetTableName()!;

            foreach (var property in entityType.GetProperties())
            {
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(SnakeCase.Convert(property.Name));
                }
            }

            foreach (var key in entityType.GetKeys())
            {
                key.SetName(key.IsPrimaryKey() ? $"pk_{table}" : $"ak_{table}_{Columns(key.Properties)}");
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName($"fk_{table}_{Columns(foreignKey.Properties)}");
            }

            foreach (var index in entityType.GetIndexes())
            {
                if (index.FindAnnotation(RelationalAnnotationNames.Name) is null)
                {
                    index.SetDatabaseName($"{(index.IsUnique ? "ux" : "ix")}_{table}_{Columns(index.Properties)}");
                }
            }
        }
    }

    private static string Columns(IEnumerable<IMutableProperty> properties) =>
        string.Join("_", properties.Select(property => property.GetColumnName()));
}

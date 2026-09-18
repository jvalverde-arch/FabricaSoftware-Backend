using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Application.Traceability.Contracts;

namespace SoftwareFactory.Application.Tests.Traceability;

/// <summary>
/// The two ordered lists the tree of HU-004 is drawn with. They are data that travels to the SQL as a parameter, so
/// what keeps them honest is here: a type added to the catalog without a place in the order, or a hierarchical
/// relation that the compatibility matrix does not even know, has to fail here and not in front of a user.
/// </summary>
public sealed class HierarchyCatalogTests
{
    [Fact]
    public void The_catalog_order_covers_the_types_that_have_a_schema_and_nothing_else()
    {
        var registry = ArtifactSchemaRegistry.Embedded.Types.Order();

        Assert.Equal(registry, ArtifactTypeCatalog.InOrder.Order());
    }

    [Fact]
    public void The_catalog_order_has_no_repeats() =>
        Assert.Equal(ArtifactTypeCatalog.InOrder.Count, ArtifactTypeCatalog.InOrder.Distinct(StringComparer.Ordinal).Count());

    [Fact]
    public void The_tree_hangs_its_first_level_on_a_type_of_the_catalog() =>
        Assert.Contains(ArtifactTypeCatalog.Module, ArtifactTypeCatalog.InOrder, StringComparer.Ordinal);

    [Fact]
    public void Every_hierarchical_relation_is_a_relation_the_platform_knows() =>
        Assert.All(HierarchyRelations.ByPrecedence, type => Assert.True(RelationCompatibilityMatrix.Embedded.Knows(type)));

    [Fact]
    // The order is not decoration: it settles the edge type of a pair and sorts the children of a node, and the SQL
    // reads it from this very list, so changing it here changes the tree.
    public void The_precedence_is_the_one_the_hierarchy_model_declares() =>
        Assert.Equal([RelationNames.BelongsTo, RelationNames.Implements, RelationNames.Validates], HierarchyRelations.ByPrecedence);
}

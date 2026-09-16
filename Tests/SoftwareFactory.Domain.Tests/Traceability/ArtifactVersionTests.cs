using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Domain.Tests.Traceability;

/// <summary>
/// A version is an immutable snapshot (HU-001 §2) and it records the schema version its content was written
/// against: without that number, old content cannot be interpreted once the type's schema evolves.
/// </summary>
public sealed class ArtifactVersionTests
{
    [Fact]
    public void A_version_keeps_its_content_author_and_schema_version()
    {
        var tenantId = Guid.CreateVersion7();
        var artifactId = Guid.CreateVersion7();
        var authorId = Guid.CreateVersion7();

        var version = new ArtifactVersion(tenantId, artifactId, number: 1, """{"as_a":"usuaria"}""", schemaVersion: 2, AuthorType.Agent, authorId);

        Assert.Equal(artifactId, version.ArtifactId);
        Assert.Equal(1, version.Number);
        Assert.Equal(2, version.SchemaVersion);
        Assert.Equal(AuthorType.Agent, version.AuthorType);
        Assert.Equal(authorId, version.AuthorId);
        Assert.Equal("""{"as_a":"usuaria"}""", version.Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_version_number_starts_at_one(int number) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewVersion(number: number));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void The_schema_version_starts_at_one(int schemaVersion) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NewVersion(schemaVersion: schemaVersion));

    [Fact]
    public void The_content_must_be_a_json_document() =>
        Assert.Throws<ArgumentException>(() => NewVersion(content: "esto no es json"));

    private static ArtifactVersion NewVersion(int number = 1, int schemaVersion = 1, string content = """{"a":1}""") =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), number, content, schemaVersion, AuthorType.Human, Guid.CreateVersion7());
}

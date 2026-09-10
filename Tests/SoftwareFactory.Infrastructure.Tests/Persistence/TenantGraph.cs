using SoftwareFactory.Domain.Brain;
using SoftwareFactory.Domain.Common;
using SoftwareFactory.Domain.Finops;
using SoftwareFactory.Domain.Platform;
using SoftwareFactory.Domain.Project;
using SoftwareFactory.Domain.Traceability;
using SoftwareFactory.Infrastructure.Persistence;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

/// <summary>Arranges one row (two for artifact) in every table for a fresh tenant, through the owner connection.</summary>
internal static class TenantGraph
{
    public static async Task<Guid> CreateAsync(SoftwareFactoryDbContext admin)
    {
        var slug = "t-" + Guid.NewGuid().ToString("N");
        var tenant = new Tenant($"Tenant {slug}", slug);
        var user = new AppUser(tenant.Id, $"user@{slug}.test", "Test user", "$argon2id$v=19$m=8,t=1,p=1$c2FsdHNhbHRzYWx0c2FsdA$aGFzaA");
        var role = new UserRole(tenant.Id, user.Id, Role.Functional);
        var project = new SoftwareProject(tenant.Id, $"Project {slug}", "Probe project");
        var story = new Artifact(tenant.Id, project.Id, "user_story", "As a user", ArtifactLevel.Project);
        var testCase = new Artifact(tenant.Id, project.Id, "test_case", "Login works", ArtifactLevel.Project);
        var version = new ArtifactVersion(tenant.Id, story.Id, story.AdvanceVersion(), """{"title":"As a user"}""", AuthorType.Human, user.Id);
        var relation = new Relation(tenant.Id, testCase.Id, story.Id, "validates", """{"note":"probe"}""", AuthorType.Agent, user.Id);
        var decision = new Decision(tenant.Id, project.Id, DecisionType.Decision, AuthorType.Human, user.Id, Role.Functional, "Because the probe says so.", null);
        var job = new Job(tenant.Id, "probe", """{"n":1}""");
        var call = new LlmCall(tenant.Id, job.Id, "stub", "stub-small", 10, 5, 0, 0, 120, 0.000123m);
        var document = new SourceDocument(tenant.Id, "Norma", "text/plain", $"{slug}/norma.txt", 42, "https://example.test/norma");
        var chunk = new Chunk(tenant.Id, document.Id, 0, "Texto del fragmento", "p. 1");
        chunk.SetEmbedding(Embedding(0.25f));

        admin.AddRange(tenant, user, role, project, story, testCase, version, relation, decision, job, call, document, chunk);
        await admin.SaveChangesAsync();

        return tenant.Id;
    }

    public static ReadOnlyMemory<float> Embedding(float value)
    {
        var values = new float[EmbeddingVector.Dimensions];
        Array.Fill(values, value);
        return values;
    }
}

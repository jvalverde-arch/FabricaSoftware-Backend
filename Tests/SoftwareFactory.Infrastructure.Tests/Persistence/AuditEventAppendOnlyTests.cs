using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Tests.Persistence;

/// <summary>The application role can add and read audit events but never change or remove them (doc 03 §5: append-only).</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class AuditEventAppendOnlyTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Application_role_can_insert_and_read_but_not_update_or_delete()
    {
        Guid tenantId;

        await using (var admin = fixture.CreateAdminContext())
        {
            tenantId = await TenantGraph.CreateAsync(admin);
        }

        await using var context = fixture.CreateAppContext(tenantId);
        context.AuditEvents.Add(new AuditEvent(tenantId, AuditAction.Logout, null, null, new AuditClient(null, null), DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.AuditEvents.CountAsync());

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            context.AuditEvents.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.UserAgent, "tampered")));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(() => context.AuditEvents.ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, delete.SqlState);
    }
}
